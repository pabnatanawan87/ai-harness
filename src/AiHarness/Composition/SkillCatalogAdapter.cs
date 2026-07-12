using AiHarness.Cli.Abstractions;
using AiHarness.Skills;

namespace AiHarness.Composition;

/// <summary>
/// Bridges the Skills module's <see cref="SkillLoader"/> onto the CLI's
/// <see cref="ISkillCatalog"/> seam. The loader speaks in rich <see cref="Skill"/> objects;
/// the CLI only needs the small, display-safe <see cref="SkillInfo"/>. Keeping the mapping
/// here (rather than in either module) lets both sides stay unaware of each other and be
/// tested apart, which is the whole point of the CLI abstractions.
/// </summary>
public sealed class SkillCatalogAdapter : ISkillCatalog
{
    private readonly SkillLoader _loader;

    public SkillCatalogAdapter(SkillLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    /// <inheritdoc />
    public IReadOnlyList<SkillInfo> List() =>
        _loader.LoadAll().Select(ToInfo).ToList();

    /// <inheritdoc />
    public SkillInfo Get(string name)
    {
        // TryLoad returns false only for a genuinely missing skill; a skill that exists but
        // is malformed still throws SkillLoadException, which is the right thing to surface.
        if (!_loader.TryLoad(name, out Skill? skill) || skill is null)
        {
            throw new SkillNotFoundException(name);
        }

        return ToInfo(skill);
    }

    private static SkillInfo ToInfo(Skill skill) => new(
        skill.Name,
        skill.Description,
        skill.Inputs,
        skill.Output,
        skill.Directory);
}
