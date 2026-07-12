using AiHarness.Cli.Abstractions;
using AiHarness.Rendering;

using Microsoft.Extensions.AI;

namespace AiHarness.Composition;

/// <summary>
/// Bridges the Rendering module's <see cref="PostProcessor"/> onto the CLI's
/// <see cref="IPostProcessor"/> seam for a single-shot run: take the model's text, and
/// either write it to the requested --out path or hand it back for printing.
///
/// The richer structured-output path (JSON with a one-retry policy, multi-file writes)
/// lives on <see cref="PostProcessor"/> itself and is what a future multi-step runner will
/// call; this adapter deliberately keeps the CLI's last stage small and predictable.
/// </summary>
public sealed class PostProcessorAdapter : IPostProcessor
{
    /// <inheritdoc />
    public async Task<PostProcessResult> ProcessAsync(
        SkillInfo skill,
        ChatResponse response,
        RunOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(response);
        ArgumentNullException.ThrowIfNull(options);

        string text = (response.Text ?? string.Empty).TrimEnd();

        if (!string.IsNullOrWhiteSpace(options.OutPath))
        {
            string path = await PostProcessor
                .WriteFileAsync(options.OutPath, text + "\n", cancellationToken)
                .ConfigureAwait(false);
            return new PostProcessResult(text, path);
        }

        return new PostProcessResult(text, null);
    }
}
