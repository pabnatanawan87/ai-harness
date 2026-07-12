using System.Text;
using System.Text.RegularExpressions;

using AiHarness.Context;

namespace AiHarness.Rendering;

/// <summary>
/// Turns a prompt template plus a set of named inputs and gathered context blocks into
/// the final prompt string sent to the model.
///
/// Placeholders use double braces: <c>{{name}}</c>. Double braces are deliberate so that
/// prompt templates can contain literal single-brace JSON examples (a JSON schema in the
/// prompt, say) without being mangled. Two names are handled specially:
/// <list type="bullet">
///   <item><c>{{context}}</c> is replaced by the rendered context blocks. If the template
///   never mentions it, the context is appended under a "Context" heading instead.</item>
/// </list>
/// A placeholder with no matching input raises <see cref="TemplateRenderException"/> so a
/// typo in a skill template fails loudly rather than silently shipping a broken prompt.
/// </summary>
public static class PromptRenderer
{
    /// <summary>The reserved placeholder name that receives the gathered context.</summary>
    public const string ContextKey = "context";

    // Matches {{ name }} where name is an identifier-like token. Whitespace inside the
    // braces is tolerated. Anything that is not a clean token (for example a JSON snippet
    // wrapped in double braces) simply does not match and is left untouched.
    private static readonly Regex PlaceholderPattern = new(
        @"\{\{\s*(?<name>[A-Za-z0-9_.\-]+)\s*\}\}",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// Renders <paramref name="template"/> against <paramref name="inputs"/> and
    /// <paramref name="context"/>. Input keys are matched case-sensitively. The gathered
    /// context is bound to the reserved <see cref="ContextKey"/> placeholder, or appended
    /// when the template does not reference it.
    /// </summary>
    public static string Render(
        string template,
        IReadOnlyDictionary<string, string>? inputs = null,
        IReadOnlyList<ContextBlock>? context = null)
    {
        ArgumentNullException.ThrowIfNull(template);

        string contextText = RenderContext(context);
        bool templateUsesContext = TemplateReferences(template, ContextKey);

        // Build the lookup table. Gathered context owns the reserved name, overriding any
        // caller-supplied input of the same name.
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        if (inputs is not null)
        {
            foreach (KeyValuePair<string, string> pair in inputs)
            {
                values[pair.Key] = pair.Value;
            }
        }

        if (templateUsesContext)
        {
            values[ContextKey] = contextText;
        }

        string body = Substitute(template, values);

        if (!templateUsesContext && contextText.Length > 0)
        {
            var builder = new StringBuilder(body.TrimEnd());
            builder.Append("\n\n# Context\n\n").Append(contextText);
            return builder.ToString();
        }

        return body;
    }

    /// <summary>
    /// Substitutes <c>{{name}}</c> placeholders in <paramref name="template"/> using
    /// <paramref name="values"/>. This is the low-level engine shared by prompt rendering
    /// and by templating short gatherer arguments (for example an "{{symptom}}" search).
    /// </summary>
    public static string Substitute(string template, IReadOnlyDictionary<string, string> values)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(values);

        return PlaceholderPattern.Replace(template, match =>
        {
            string name = match.Groups["name"].Value;
            if (values.TryGetValue(name, out string? value))
            {
                return value;
            }

            string known = values.Count == 0
                ? "(none)"
                : string.Join(", ", values.Keys.OrderBy(k => k, StringComparer.Ordinal));
            throw new TemplateRenderException(
                $"Template references unknown placeholder '{{{{{name}}}}}'. Known names: {known}.");
        });
    }

    /// <summary>Formats context blocks into a single markdown-ish string for prompt injection.</summary>
    public static string RenderContext(IReadOnlyList<ContextBlock>? context)
    {
        if (context is null || context.Count == 0)
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (int i = 0; i < context.Count; i++)
        {
            ContextBlock block = context[i];
            builder.Append("## ").Append(block.Label).Append("\n\n");
            builder.Append(block.Text.TrimEnd()).Append('\n');
            if (i < context.Count - 1)
            {
                builder.Append('\n');
            }
        }

        return builder.ToString();
    }

    private static bool TemplateReferences(string template, string name)
    {
        foreach (Match match in PlaceholderPattern.Matches(template))
        {
            if (string.Equals(match.Groups["name"].Value, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}

/// <summary>Raised when a template references a placeholder that has no supplied value.</summary>
public sealed class TemplateRenderException : Exception
{
    public TemplateRenderException(string message) : base(message)
    {
    }
}
