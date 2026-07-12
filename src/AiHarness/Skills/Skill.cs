namespace AiHarness.Skills;

/// <summary>
/// A loaded skill: the parsed manifest (skill.yaml) plus the prompt templates its steps
/// reference, read from disk. Skills are DATA, not code (DESIGN 3.2): adding one is a new
/// folder under a skills directory, no recompile. This is the in-memory shape the runner
/// consumes after <see cref="SkillLoader"/> has read and validated the files.
/// </summary>
public sealed class Skill
{
    /// <summary>Manifest file name expected inside each skill folder.</summary>
    public const string ManifestFileName = "skill.yaml";

    /// <summary>Default output format when a manifest does not set "output:".</summary>
    public const string DefaultOutput = "markdown";

    public Skill(
        string name,
        string description,
        IReadOnlyList<string> inputs,
        IReadOnlyList<ContextGatherer> context,
        IReadOnlyList<SkillStep> steps,
        string output,
        string directory)
    {
        Name = name;
        Description = description;
        Inputs = inputs;
        Context = context;
        Steps = steps;
        Output = output;
        Directory = directory;
    }

    /// <summary>Skill id, matching its folder name (e.g. "rca"). Used by "ai run &lt;name&gt;".</summary>
    public string Name { get; }

    /// <summary>One-line human description shown by "ai skills list".</summary>
    public string Description { get; }

    /// <summary>Declared input names the caller supplies (e.g. ["symptom"]). May be empty.</summary>
    public IReadOnlyList<string> Inputs { get; }

    /// <summary>Context-gathering directives to run before the first model call. May be empty.</summary>
    public IReadOnlyList<ContextGatherer> Context { get; }

    /// <summary>Ordered pipeline steps. Always at least one.</summary>
    public IReadOnlyList<SkillStep> Steps { get; }

    /// <summary>Desired output format, e.g. "markdown" or "json". Defaults to "markdown".</summary>
    public string Output { get; }

    /// <summary>Absolute path of the folder this skill was loaded from.</summary>
    public string Directory { get; }
}
