namespace AiHarness.Cli;

/// <summary>
/// Scaffolds a new skill folder for "ai new-skill". A skill is data, not code (see DESIGN
/// section 3.2), so scaffolding is just writing a starter skill.yaml and a prompt template
/// into ./skills/&lt;name&gt;. This lives with the CLI because it is a one-shot authoring
/// helper, not part of the run pipeline, and it needs nothing from the other modules.
/// </summary>
public static class SkillScaffolder
{
    /// <summary>The result of a scaffold attempt.</summary>
    /// <param name="Created">True when a new skill folder was written.</param>
    /// <param name="SkillDirectory">Absolute path to the skill folder.</param>
    /// <param name="Message">A human-readable summary to print.</param>
    public readonly record struct ScaffoldResult(bool Created, string SkillDirectory, string Message);

    /// <summary>
    /// Creates skills/&lt;name&gt;/skill.yaml and skills/&lt;name&gt;/prompts/main.md under
    /// <paramref name="baseDirectory"/> (default: the current directory). Refuses to
    /// overwrite an existing skill folder.
    /// </summary>
    public static ScaffoldResult Scaffold(string name, string? baseDirectory = null)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return new ScaffoldResult(false, string.Empty, "A skill name is required, for example: ai new-skill explain");
        }

        if (!IsValidName(name))
        {
            return new ScaffoldResult(
                false,
                string.Empty,
                $"Invalid skill name '{name}'. Use letters, digits, hyphens, and underscores only.");
        }

        string root = baseDirectory ?? Directory.GetCurrentDirectory();
        string skillsDir = Path.Combine(root, "skills");
        string skillDir = Path.Combine(skillsDir, name);

        if (Directory.Exists(skillDir))
        {
            return new ScaffoldResult(false, skillDir, $"Skill '{name}' already exists at {skillDir}.");
        }

        string promptsDir = Path.Combine(skillDir, "prompts");
        Directory.CreateDirectory(promptsDir);

        File.WriteAllText(Path.Combine(skillDir, "skill.yaml"), ManifestTemplate(name));
        File.WriteAllText(Path.Combine(promptsDir, "main.md"), PromptTemplate());

        return new ScaffoldResult(true, skillDir, $"Created skill '{name}' at {skillDir}.");
    }

    private static bool IsValidName(string name)
    {
        foreach (char c in name)
        {
            bool ok = char.IsLetterOrDigit(c) || c == '-' || c == '_';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    private static string ManifestTemplate(string name) =>
        $"""
        name: {name}
        description: TODO describe what this skill does in one line.
        inputs: [input]
        context:
          - repo_map
        steps:
          - prompt: prompts/main.md
        output: markdown
        """ + "\n";

    private static string PromptTemplate() =>
        """
        You are a careful, concise engineering assistant.

        Task:
        {{input}}

        Repository context:
        {{repo_map}}

        Answer in Markdown. Be specific and cite file paths where relevant.
        """ + "\n";
}
