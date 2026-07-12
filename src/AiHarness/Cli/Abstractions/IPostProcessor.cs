using Microsoft.Extensions.AI;

namespace AiHarness.Cli.Abstractions;

/// <summary>
/// The outcome of post-processing a model response: the final text to show or store, and
/// the path it was written to (null when the result was not written to a file).
/// </summary>
/// <param name="Text">The final, post-processed text of the run.</param>
/// <param name="WrittenPath">Absolute path the text was written to, or null if not written.</param>
public sealed record PostProcessResult(string Text, string? WrittenPath);

/// <summary>
/// The CLI's view of post-processing. Implemented by the PostProcessor module, which turns
/// a raw model <see cref="ChatResponse"/> into the final artifact: extract/validate any
/// structured output, apply the skill's declared output format, and write to
/// <see cref="RunOptions.OutPath"/> when one was given. The CLI depends only on this
/// interface so the last stage of a run can be swapped or faked.
/// </summary>
public interface IPostProcessor
{
    /// <summary>
    /// Processes the model <paramref name="response"/> for a skill run and returns the
    /// final text plus any file it was written to.
    /// </summary>
    Task<PostProcessResult> ProcessAsync(
        SkillInfo skill,
        ChatResponse response,
        RunOptions options,
        CancellationToken cancellationToken);
}
