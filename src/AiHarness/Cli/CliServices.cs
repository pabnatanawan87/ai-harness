using AiHarness.Cli.Abstractions;

namespace AiHarness.Cli;

/// <summary>
/// The set of collaborators the CLI needs to run skills, gathered in one place so the
/// composition root can build them once and hand them to <see cref="CliApplication"/>.
///
/// The concrete implementations live in their own modules - the Skills, Context, and
/// Rendering modules plus the post-processor - and are wired together at the entry point.
/// The CLI itself depends only on the interfaces, which keeps every command testable with
/// fakes and no live model calls (see DESIGN section 8).
/// </summary>
/// <param name="SkillCatalog">Resolves and lists skills (implemented by SkillLoader).</param>
/// <param name="ContextGatherer">Assembles run context (implemented by ContextGatherer).</param>
/// <param name="PromptRenderer">Renders prompt messages (implemented by PromptRenderer).</param>
/// <param name="PostProcessor">Finalizes and optionally writes the result (implemented by PostProcessor).</param>
public sealed record CliServices(
    ISkillCatalog SkillCatalog,
    IContextGatherer ContextGatherer,
    IPromptRenderer PromptRenderer,
    IPostProcessor PostProcessor);
