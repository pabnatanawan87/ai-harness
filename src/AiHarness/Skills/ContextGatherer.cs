namespace AiHarness.Skills;

/// <summary>
/// One context-gathering directive declared by a skill (DESIGN 3.2). A skill lists these
/// under "context:" so the runner can collect material - files, a repo map, a ripgrep
/// search, the current diff - before the model is called.
///
/// In skill.yaml a directive is written either as a bare name or as a single-key mapping
/// carrying an argument:
/// <code>
/// context:
///   - repo_map                 # Name="repo_map", Argument=null
///   - ripgrep: "{keywords}"    # Name="ripgrep",  Argument="{keywords}"
/// </code>
/// The set of gatherer names a runner actually understands (files, diff, ripgrep,
/// repo_map, ...) is code that lives elsewhere; this type is only the parsed declaration.
/// </summary>
public sealed record ContextGatherer(string Name, string? Argument)
{
    /// <summary>True when the directive carried an argument (the mapping form).</summary>
    public bool HasArgument => !string.IsNullOrEmpty(Argument);
}
