using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AiHarness.Skills;

/// <summary>
/// Discovers and loads data-defined skills (DESIGN 3.2). A skill is a folder that contains
/// a <c>skill.yaml</c> manifest plus the prompt template files its steps reference. The
/// loader reads the YAML with YamlDotNet, resolves each step's prompt path relative to the
/// skill folder, reads the template text, and validates the result. No skill is code, so
/// adding one never requires a recompile.
///
/// Skills are looked up across an ordered list of search directories. The first directory
/// that contains a folder matching the skill name wins, which lets a project-local skill
/// override a personal one, and a personal one override the built-ins shipped with the
/// tool. The default order is:
/// <list type="number">
///   <item><description><c>&lt;cwd&gt;/skills</c> - project-local skills.</description></item>
///   <item><description><c>&lt;user home&gt;/.ai-harness/skills</c> - personal skills.</description></item>
///   <item><description><c>&lt;app dir&gt;/skills</c> - the built-in skills shipped with the tool.</description></item>
/// </list>
/// </summary>
public sealed class SkillLoader
{
    /// <summary>Conventional name of the per-user skills directory under the home folder.</summary>
    public const string UserSkillsFolderName = ".ai-harness";

    /// <summary>Conventional name of a "skills" directory in any search root.</summary>
    public const string SkillsDirectoryName = "skills";

    private readonly IReadOnlyList<string> _searchDirectories;
    private readonly IDeserializer _deserializer;

    /// <summary>
    /// Creates a loader over an explicit, ordered list of skills directories. Directories
    /// that do not exist are simply skipped at lookup time, so passing a not-yet-created
    /// user directory is fine.
    /// </summary>
    public SkillLoader(IEnumerable<string> searchDirectories)
    {
        ArgumentNullException.ThrowIfNull(searchDirectories);

        _searchDirectories = searchDirectories
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Select(Path.GetFullPath)
            .ToList();

        // Manifest keys are lower-case single words (name, description, inputs, context,
        // steps, prompt, foreach, output); camelCase matches them onto our DTO properties.
        // Unmatched keys are ignored so future manifest fields do not break older builds.
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    /// <summary>The resolved, absolute search directories, in precedence order.</summary>
    public IReadOnlyList<string> SearchDirectories => _searchDirectories;

    /// <summary>
    /// Builds a loader over the default search directories (project-local, then per-user,
    /// then the built-ins shipped alongside the tool). See the type remarks for the order.
    /// </summary>
    public static SkillLoader CreateDefault()
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var directories = new List<string>
        {
            Path.Combine(Directory.GetCurrentDirectory(), SkillsDirectoryName),
        };

        if (!string.IsNullOrWhiteSpace(home))
        {
            directories.Add(Path.Combine(home, UserSkillsFolderName, SkillsDirectoryName));
        }

        // Skills bundled with the packaged tool live next to the assembly.
        directories.Add(Path.Combine(AppContext.BaseDirectory, SkillsDirectoryName));

        return new SkillLoader(directories);
    }

    /// <summary>
    /// Loads a single skill by name (its folder name), searching the directories in order
    /// and returning the first match. Throws <see cref="SkillLoadException"/> when no skill
    /// of that name exists or when its files are malformed.
    /// </summary>
    public Skill Load(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string? folder = FindSkillFolder(name);
        if (folder is null)
        {
            throw new SkillLoadException(
                $"Skill '{name}' was not found in any skills directory ({DescribeSearchPath()}).");
        }

        return LoadFromFolder(folder);
    }

    /// <summary>
    /// Attempts to load a skill by name. Returns false (rather than throwing) when no skill
    /// of that name is found. A skill that is found but malformed still throws
    /// <see cref="SkillLoadException"/>, since that is a real error the user should see.
    /// </summary>
    public bool TryLoad(string name, out Skill? skill)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        string? folder = FindSkillFolder(name);
        if (folder is null)
        {
            skill = null;
            return false;
        }

        skill = LoadFromFolder(folder);
        return true;
    }

    /// <summary>
    /// Loads every discoverable skill across all search directories, de-duplicated by name
    /// (first directory wins) and ordered by name. A folder without a skill.yaml is ignored;
    /// a folder with a malformed skill.yaml throws <see cref="SkillLoadException"/>.
    /// </summary>
    public IReadOnlyList<Skill> LoadAll()
    {
        var byName = new Dictionary<string, Skill>(StringComparer.OrdinalIgnoreCase);

        foreach (string root in _searchDirectories)
        {
            if (!Directory.Exists(root))
            {
                continue;
            }

            foreach (string folder in Directory.EnumerateDirectories(root))
            {
                if (!File.Exists(Path.Combine(folder, Skill.ManifestFileName)))
                {
                    continue;
                }

                Skill skill = LoadFromFolder(folder);

                // First search directory that defines a name wins; keep it.
                if (!byName.ContainsKey(skill.Name))
                {
                    byName.Add(skill.Name, skill);
                }
            }
        }

        return byName.Values
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private string? FindSkillFolder(string name)
    {
        foreach (string root in _searchDirectories)
        {
            string candidate = Path.Combine(root, name);
            if (File.Exists(Path.Combine(candidate, Skill.ManifestFileName)))
            {
                return candidate;
            }
        }

        return null;
    }

    private Skill LoadFromFolder(string folder)
    {
        string manifestPath = Path.Combine(folder, Skill.ManifestFileName);

        string yaml;
        try
        {
            yaml = File.ReadAllText(manifestPath);
        }
        catch (IOException ex)
        {
            throw new SkillLoadException($"Could not read '{manifestPath}': {ex.Message}", ex);
        }

        SkillManifest? manifest;
        try
        {
            manifest = _deserializer.Deserialize<SkillManifest>(yaml);
        }
        catch (YamlException ex)
        {
            throw new SkillLoadException($"Could not parse '{manifestPath}': {ex.Message}", ex);
        }

        if (manifest is null)
        {
            throw new SkillLoadException($"'{manifestPath}' is empty.");
        }

        string name = (manifest.Name ?? string.Empty).Trim();
        if (name.Length == 0)
        {
            throw new SkillLoadException($"'{manifestPath}' is missing a 'name'.");
        }

        IReadOnlyList<ContextGatherer> context = BuildContext(manifest.Context, manifestPath);
        IReadOnlyList<SkillStep> steps = BuildSteps(manifest.Steps, folder, manifestPath);

        string description = (manifest.Description ?? string.Empty).Trim();
        string output = string.IsNullOrWhiteSpace(manifest.Output)
            ? Skill.DefaultOutput
            : manifest.Output.Trim();

        IReadOnlyList<string> inputs = manifest.Inputs is null
            ? Array.Empty<string>()
            : manifest.Inputs
                .Where(i => !string.IsNullOrWhiteSpace(i))
                .Select(i => i.Trim())
                .ToList();

        return new Skill(name, description, inputs, context, steps, output, folder);
    }

    private static IReadOnlyList<ContextGatherer> BuildContext(List<object>? rawContext, string manifestPath)
    {
        if (rawContext is null || rawContext.Count == 0)
        {
            return Array.Empty<ContextGatherer>();
        }

        var gatherers = new List<ContextGatherer>(rawContext.Count);
        foreach (object item in rawContext)
        {
            gatherers.Add(ToGatherer(item, manifestPath));
        }

        return gatherers;
    }

    private static ContextGatherer ToGatherer(object item, string manifestPath)
    {
        switch (item)
        {
            // Bare name form:  - repo_map
            case string name when !string.IsNullOrWhiteSpace(name):
                return new ContextGatherer(name.Trim(), null);

            // Single-key mapping form:  - ripgrep: "{keywords}"
            case IDictionary<object, object> map when map.Count == 1:
                KeyValuePair<object, object> entry = map.First();
                string key = entry.Key?.ToString()?.Trim() ?? string.Empty;
                if (key.Length == 0)
                {
                    throw new SkillLoadException($"'{manifestPath}' has a context entry with an empty name.");
                }

                string? argument = entry.Value?.ToString()?.Trim();
                return new ContextGatherer(key, argument);

            default:
                throw new SkillLoadException(
                    $"'{manifestPath}' has an invalid 'context' entry. Each entry must be a name " +
                    "(e.g. repo_map) or a single-key mapping (e.g. ripgrep: \"...\").");
        }
    }

    private static IReadOnlyList<SkillStep> BuildSteps(List<StepManifest>? rawSteps, string folder, string manifestPath)
    {
        if (rawSteps is null || rawSteps.Count == 0)
        {
            throw new SkillLoadException($"'{manifestPath}' must declare at least one step under 'steps'.");
        }

        var steps = new List<SkillStep>(rawSteps.Count);
        for (int i = 0; i < rawSteps.Count; i++)
        {
            StepManifest raw = rawSteps[i];
            string promptPath = (raw.Prompt ?? string.Empty).Trim();
            if (promptPath.Length == 0)
            {
                throw new SkillLoadException($"'{manifestPath}' step {i + 1} is missing a 'prompt'.");
            }

            string template = ReadPromptTemplate(folder, promptPath, i + 1, manifestPath);
            string? forEach = string.IsNullOrWhiteSpace(raw.Foreach) ? null : raw.Foreach.Trim();

            steps.Add(new SkillStep(promptPath, template, forEach));
        }

        return steps;
    }

    private static string ReadPromptTemplate(string folder, string promptPath, int stepNumber, string manifestPath)
    {
        // Manifests use forward slashes; normalize for the host filesystem.
        string relative = promptPath.Replace('/', Path.DirectorySeparatorChar);
        string fullPath = Path.Combine(folder, relative);

        if (!File.Exists(fullPath))
        {
            throw new SkillLoadException(
                $"'{manifestPath}' step {stepNumber} references prompt '{promptPath}', but no file exists at '{fullPath}'.");
        }

        try
        {
            return File.ReadAllText(fullPath);
        }
        catch (IOException ex)
        {
            throw new SkillLoadException($"Could not read prompt '{fullPath}': {ex.Message}", ex);
        }
    }

    private string DescribeSearchPath() => string.Join(", ", _searchDirectories);

    /// <summary>
    /// Raw shape of a skill.yaml document, used only for deserialization. Every field is
    /// optional here; <see cref="SkillLoader"/> validates and converts it into the public
    /// <see cref="Skill"/> model with clear errors.
    /// </summary>
    private sealed class SkillManifest
    {
        public string? Name { get; set; }
        public string? Description { get; set; }
        public List<string>? Inputs { get; set; }

        // Heterogeneous: each entry is either a scalar string or a single-key mapping.
        public List<object>? Context { get; set; }

        public List<StepManifest>? Steps { get; set; }
        public string? Output { get; set; }
    }

    /// <summary>Raw shape of one entry under "steps:". Both fields are validated by the loader.</summary>
    private sealed class StepManifest
    {
        public string? Prompt { get; set; }
        public string? Foreach { get; set; }
    }
}
