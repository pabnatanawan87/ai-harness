using AiHarness.Cli.Abstractions;
using AiHarness.Context;
using AiHarness.Rendering;
using AiHarness.Skills;

using SkillContextDirective = AiHarness.Skills.ContextGatherer;

namespace AiHarness.Composition;

/// <summary>
/// Bridges the Context module's built-in gatherers onto the CLI's
/// <see cref="IContextGatherer"/> seam.
///
/// The CLI hands over a <see cref="SkillInfo"/> and the run-time flags; this adapter reloads
/// the full skill to see its declared "context:" directives, binds the user's inputs
/// (--input, --file) to the named placeholders a skill uses, substitutes those placeholders
/// into any gatherer argument (for example <c>ripgrep: "{{symptom}}"</c>), runs each
/// gatherer, and returns one flat map. The map carries BOTH the resolved input values and
/// the gathered blocks, keyed by the names the prompt templates reference (repo_map, diff,
/// files, ripgrep, stdin, plus the skill's declared inputs), so the renderer can fill the
/// template from this single bag.
/// </summary>
public sealed class ContextGathererAdapter : IContextGatherer
{
    private readonly SkillLoader _loader;

    public ContextGathererAdapter(SkillLoader loader)
    {
        _loader = loader ?? throw new ArgumentNullException(nameof(loader));
    }

    /// <inheritdoc />
    public async Task<GatheredContext> GatherAsync(
        SkillInfo skill,
        RunOptions options,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(skill);
        ArgumentNullException.ThrowIfNull(options);

        Skill full = _loader.Load(skill.Name);
        string workingDirectory = Directory.GetCurrentDirectory();

        // Resolved inputs first: they are both the arguments for gatherers (e.g. the ripgrep
        // pattern) and values the prompt template can reference directly (e.g. {{symptom}}).
        Dictionary<string, string> values = BuildInputValues(full, options);
        var items = new Dictionary<string, string>(values, StringComparer.Ordinal);

        foreach (SkillContextDirective directive in full.Context)
        {
            string? argument = directive.HasArgument
                ? PromptRenderer.Substitute(directive.Argument!, values)
                : null;

            var spec = new ContextSpec(directive.Name, argument);
            ContextBlock block = await AiHarness.Context.ContextGatherer
                .GatherOneAsync(spec, workingDirectory, cancellationToken)
                .ConfigureAwait(false);

            items[NormalizeKey(directive.Name)] = block.Text;
        }

        // Honor --diff even for a skill that did not declare a diff gatherer.
        if (options.IncludeDiff && !items.ContainsKey("diff"))
        {
            ContextBlock diff = await AiHarness.Context.ContextGatherer
                .GitDiffAsync(staged: false, workingDirectory, cancellationToken)
                .ConfigureAwait(false);
            items["diff"] = diff.Text;
        }

        return new GatheredContext(items);
    }

    // Seeds every declared input with an empty string so a template placeholder always
    // resolves (a run that omits an input renders it as empty rather than crashing), then
    // binds the actual flags: --file to "file", --input to "input" and to each declared input.
    private static Dictionary<string, string> BuildInputValues(Skill skill, RunOptions options)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (string name in skill.Inputs)
        {
            values[name] = string.Empty;
        }

        if (options.Input is not null)
        {
            values["input"] = options.Input;
        }

        if (options.FilePath is not null)
        {
            values["file"] = options.FilePath;
        }

        foreach (string name in skill.Inputs)
        {
            if (name == "file" && options.FilePath is not null)
            {
                values[name] = options.FilePath;
            }
            else if (options.Input is not null)
            {
                values[name] = options.Input;
            }
        }

        return values;
    }

    // Collapse gatherer aliases onto the canonical key the prompt templates reference.
    private static string NormalizeKey(string kind) => kind.Trim().ToLowerInvariant() switch
    {
        "files" or "file" => "files",
        "diff" or "git" or "git_diff" or "gitdiff" => "diff",
        "ripgrep" or "rg" or "grep" => "ripgrep",
        "repo_map" or "repomap" or "map" => "repo_map",
        "stdin" or "-" => "stdin",
        var other => other,
    };
}
