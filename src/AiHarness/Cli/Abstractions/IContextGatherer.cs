namespace AiHarness.Cli.Abstractions;

/// <summary>
/// The context a skill run has assembled before the prompt is rendered. It is a simple
/// bag of named blocks (for example "file", "diff", "input", "repo_map") that the prompt
/// template can interpolate. Keeping it a plain string-to-string map keeps the renderer
/// trivial and the whole pipeline easy to reason about.
/// </summary>
/// <param name="Items">Named context blocks, keyed by the name a template refers to.</param>
public sealed record GatheredContext(IReadOnlyDictionary<string, string> Items)
{
    /// <summary>An empty context, useful for skills that need no gathered input.</summary>
    public static GatheredContext Empty { get; } =
        new(new Dictionary<string, string>());
}

/// <summary>
/// The CLI's view of context gathering. Implemented by the Context module
/// (ContextGatherer), which honors the skill's declared "context:" entries and the
/// run-time flags (file, diff, input) to build the context blocks. The CLI depends only
/// on this interface so runs can be tested with canned context.
/// </summary>
public interface IContextGatherer
{
    /// <summary>
    /// Gathers all context blocks a skill needs for one run, combining the skill's
    /// declared context sources with the user-supplied <paramref name="options"/>.
    /// </summary>
    Task<GatheredContext> GatherAsync(
        SkillInfo skill,
        RunOptions options,
        CancellationToken cancellationToken);
}
