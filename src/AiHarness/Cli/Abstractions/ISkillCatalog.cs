namespace AiHarness.Cli.Abstractions;

/// <summary>
/// The CLI's view of the skill store. Implemented by the Skills module (SkillLoader),
/// which reads skill.yaml manifests from the project's ./skills folder and a user skills
/// directory. The CLI depends only on this interface so it can be exercised in tests with
/// a fake catalog and no filesystem.
/// </summary>
public interface ISkillCatalog
{
    /// <summary>All discovered skills, in a stable order suitable for listing.</summary>
    IReadOnlyList<SkillInfo> List();

    /// <summary>
    /// Resolves a single skill by name. Throws <see cref="SkillNotFoundException"/> when
    /// no skill with that name exists in any searched directory.
    /// </summary>
    SkillInfo Get(string name);
}

/// <summary>Raised when a requested skill name cannot be resolved in any skills directory.</summary>
public sealed class SkillNotFoundException : Exception
{
    public SkillNotFoundException(string name)
        : base($"No skill named '{name}' was found. Run 'ai skills list' to see available skills.")
    {
        SkillName = name;
    }

    /// <summary>The skill name that could not be resolved.</summary>
    public string SkillName { get; }
}
