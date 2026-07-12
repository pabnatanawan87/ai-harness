using Microsoft.Extensions.AI;

namespace AiHarness.Cli.Abstractions;

/// <summary>
/// The CLI's view of prompt rendering. Implemented by the Rendering module
/// (PromptRenderer), which fills a skill's prompt template(s) with the gathered context
/// and produces the message list to send to the model. Returning
/// <see cref="ChatMessage"/> keeps the vendor-neutral seam intact: the CLI hands these
/// straight to <see cref="IChatClient"/> without knowing anything about a vendor.
/// </summary>
public interface IPromptRenderer
{
    /// <summary>
    /// Renders the chat messages (typically a system message plus a user message) for a
    /// skill run from its template and the gathered <paramref name="context"/>.
    /// </summary>
    IReadOnlyList<ChatMessage> Render(SkillInfo skill, GatheredContext context);
}
