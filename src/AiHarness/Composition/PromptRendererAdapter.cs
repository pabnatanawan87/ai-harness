using AiHarness.Cli.Abstractions;
using AiHarness.Rendering;
using AiHarness.Skills;

using Microsoft.Extensions.AI;

namespace AiHarness.Composition;

/// <summary>
/// Bridges the Rendering module's <see cref="PromptRenderer"/> onto the CLI's
/// <see cref="IPromptRenderer"/> seam.
///
/// This is a single-shot renderer: it fills the skill's FIRST step template with the
/// gathered values and returns one user message. Multi-step and foreach orchestration
/// (DESIGN 3.2) is a later milestone; the seam is shaped so a future runner can replace
/// this adapter without touching the CLI. The gathered map already contains both the
/// resolved inputs and the context blocks, so rendering is a straight placeholder
/// substitution.
/// </summary>
public sealed class PromptRendererAdapter : IPromptRenderer
{
    private readonly SkillLoader _loader;

    public PromptRendererAdapter(SkillLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    /// <inheritdoc />
    public IReadOnlyList<ChatMessage> Render(SkillInfo skill, GatheredContext context)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(context);

        Skill full = _loader.Load(skill.Name);
        SkillStep first = full.Steps[0];

        string prompt = PromptRenderer.Substitute(first.PromptTemplate, context.Items);
        return new[] { new ChatMessage(ChatRole.User, prompt) };
    }
}
