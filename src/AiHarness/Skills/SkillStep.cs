namespace AiHarness.Skills;

/// <summary>
/// One step in a skill's pipeline (DESIGN 3.2). Each step renders a prompt template and
/// calls the model once, or - when <see cref="ForEach"/> is set - once per item of a
/// named collection produced by an earlier step.
///
/// In skill.yaml:
/// <code>
/// steps:
///   - prompt: prompts/decompose.md      # single-shot, produces e.g. hypotheses[]
///   - foreach: hypotheses               # fan out over that collection
///     prompt: prompts/verify.md
/// </code>
/// The loader resolves <see cref="PromptPath"/> relative to the skill folder and reads the
/// template text eagerly into <see cref="PromptTemplate"/>, so a loaded step is
/// self-contained and needs no further disk access to render.
/// </summary>
public sealed record SkillStep(string PromptPath, string PromptTemplate, string? ForEach)
{
    /// <summary>
    /// True when this step fans out over a collection (the "foreach:" form) rather than
    /// running once. <see cref="ForEach"/> names the collection to iterate.
    /// </summary>
    public bool IsForEach => !string.IsNullOrEmpty(ForEach);
}
