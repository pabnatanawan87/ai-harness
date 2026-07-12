using AiHarness.Cli;
using AiHarness.Skills;

namespace AiHarness.Composition;

/// <summary>
/// The composition root: it builds the concrete modules (the skill loader and the adapters
/// that wrap the Context, Rendering, and PostProcessor modules) and assembles them into the
/// <see cref="CliServices"/> the CLI consumes. This is the one place the real modules are
/// wired together; everything downstream depends only on the CLI abstractions, so the whole
/// command surface can be rebuilt with fakes in a test.
/// </summary>
public static class HarnessComposition
{
    /// <summary>
    /// Builds the fully wired services over the default skill search directories
    /// (project-local, then per-user, then the built-ins shipped with the tool).
    /// </summary>
    public static CliServices Build()
    {
        SkillLoader loader = SkillLoader.CreateDefault();
        return Build(loader);
    }

    /// <summary>Builds the services over an explicit skill loader (used by tests).</summary>
    public static CliServices Build(SkillLoader loader)
    {
        ArgumentNullException.ThrowIfNull(loader);

        return new CliServices(
            SkillCatalog: new SkillCatalogAdapter(loader),
            ContextGatherer: new ContextGathererAdapter(loader),
            PromptRenderer: new PromptRendererAdapter(loader),
            PostProcessor: new PostProcessorAdapter());
    }
}
