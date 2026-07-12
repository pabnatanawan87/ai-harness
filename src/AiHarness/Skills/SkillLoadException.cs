namespace AiHarness.Skills;

/// <summary>
/// Raised when a skill cannot be found or its files are malformed: no skill.yaml, an
/// unparseable manifest, a missing "name", no steps, a step without a prompt, or a
/// referenced prompt template that is not on disk. The message is meant to be shown
/// directly to the user running the CLI.
/// </summary>
public sealed class SkillLoadException : Exception
{
    public SkillLoadException(string message) : base(message)
    {
    }

    public SkillLoadException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
