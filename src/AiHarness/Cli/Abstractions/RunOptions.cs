namespace AiHarness.Cli.Abstractions;

/// <summary>
/// The inputs a user supplies to a single "ai run" invocation, gathered from the command
/// line flags. These are passed to the context gatherer and post-processor so a skill can
/// be fed a file, the working-tree diff, and/or a free-text prompt, and so its result can
/// be written to a chosen path.
/// </summary>
/// <param name="FilePath">Value of --file: a path whose contents become context. Null when absent.</param>
/// <param name="IncludeDiff">Value of --diff: include the working-tree/staged diff as context.</param>
/// <param name="Input">Value of --input: free-text input for the skill's primary input. Null when absent.</param>
/// <param name="OutPath">Value of --out: where to write the result. Null means print to stdout.</param>
public sealed record RunOptions(
    string? FilePath,
    bool IncludeDiff,
    string? Input,
    string? OutPath);
