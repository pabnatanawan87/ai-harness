namespace AiHarness.Cli.Abstractions;

/// <summary>
/// A display-safe description of a skill, as the CLI needs it for "skills list",
/// "skills show", and to drive a run. It is deliberately small: the CLI never needs
/// the parsed step graph or template bodies, only the metadata to show a user and a
/// path back to the skill folder on disk.
///
/// The Skills module (SkillLoader) produces these; the CLI only consumes them. Keeping
/// this a plain record means the loader and the CLI can be built and tested apart.
/// </summary>
/// <param name="Name">The skill id, e.g. "explain". Unique within a skills directory.</param>
/// <param name="Description">One-line human description from the manifest.</param>
/// <param name="Inputs">Named inputs the skill expects, e.g. ["symptom"]. May be empty.</param>
/// <param name="OutputFormat">Declared output format, e.g. "markdown" or "json". May be empty.</param>
/// <param name="SourcePath">Absolute path to the folder that holds this skill's skill.yaml.</param>
public sealed record SkillInfo(
    string Name,
    string Description,
    IReadOnlyList<string> Inputs,
    string OutputFormat,
    string SourcePath);
