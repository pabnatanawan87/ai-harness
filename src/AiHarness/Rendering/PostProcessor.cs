using System.Text.Json;

using Microsoft.Extensions.AI;

using Spectre.Console;

namespace AiHarness.Rendering;

/// <summary>
/// The tail of a run: it turns a model response into something usable. Three jobs:
/// <list type="number">
///   <item>Structured output - ask the model for JSON and parse it, with a single
///   corrective retry if the first reply is not valid JSON.</item>
///   <item>Write files - persist output text (a single result, or a set of generated
///   files) to disk, creating parent directories as needed.</item>
///   <item>Print - render text to the terminal via Spectre.Console, treating model output
///   as literal so stray markup cannot break rendering.</item>
/// </list>
/// The one-retry policy keeps the run honest without an unbounded loop: models occasionally
/// wrap JSON in prose or a code fence, and a single targeted nudge fixes almost all of it.
/// All model calls go through <see cref="IChatClient"/> - the tool's single vendor seam.
/// </summary>
public sealed class PostProcessor
{
    private readonly IChatClient _client;

    /// <summary>Creates a post-processor bound to the given chat client.</summary>
    public PostProcessor(IChatClient client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
    }

    /// <summary>
    /// Sends <paramref name="prompt"/> asking for JSON and returns the parsed root element.
    /// If the first response is not valid JSON, sends one corrective follow-up that echoes
    /// the offending text and demands raw JSON, then parses again. Throws
    /// <see cref="StructuredOutputException"/> if the second attempt also fails.
    /// </summary>
    /// <remarks>
    /// The returned <see cref="JsonElement"/> is a detached clone, so it stays valid after the
    /// underlying <see cref="JsonDocument"/> is disposed.
    /// </remarks>
    public async Task<JsonElement> GetStructuredAsync(
        string prompt,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prompt);

        // Prefer JSON response format when the caller has not asked for something else.
        ChatOptions effectiveOptions = options?.Clone() ?? new ChatOptions();
        effectiveOptions.ResponseFormat ??= ChatResponseFormat.Json;

        var messages = new List<ChatMessage>
        {
            new(ChatRole.User, prompt),
        };

        ChatResponse first = await _client
            .GetResponseAsync(messages, effectiveOptions, cancellationToken)
            .ConfigureAwait(false);

        string firstText = first.Text;
        if (TryParse(firstText, out JsonElement parsed, out _))
        {
            return parsed;
        }

        // One corrective retry. Feed back the assistant's own reply plus a strict instruction.
        messages.Add(new ChatMessage(ChatRole.Assistant, firstText));
        messages.Add(new ChatMessage(
            ChatRole.User,
            "That response was not valid JSON. Reply again with ONLY a single JSON value - "
            + "no prose, no explanation, and no Markdown code fences."));

        ChatResponse second = await _client
            .GetResponseAsync(messages, effectiveOptions, cancellationToken)
            .ConfigureAwait(false);

        string secondText = second.Text;
        if (TryParse(secondText, out parsed, out string error))
        {
            return parsed;
        }

        throw new StructuredOutputException(
            $"Model did not return valid JSON after one retry: {error}", secondText);
    }

    /// <summary>
    /// Attempts to parse <paramref name="text"/> as JSON, first as-is and then after stripping
    /// a surrounding Markdown code fence or leading/trailing prose. Returns a detached clone.
    /// </summary>
    public static bool TryParse(string text, out JsonElement element, out string error)
    {
        element = default;
        error = string.Empty;

        if (string.IsNullOrWhiteSpace(text))
        {
            error = "response was empty";
            return false;
        }

        foreach (string candidate in JsonCandidates(text))
        {
            try
            {
                using var document = JsonDocument.Parse(candidate);
                element = document.RootElement.Clone();
                return true;
            }
            catch (JsonException ex)
            {
                error = ex.Message;
            }
        }

        return false;
    }

    /// <summary>
    /// Writes <paramref name="content"/> to <paramref name="path"/>, creating parent
    /// directories as needed. Returns the absolute path written.
    /// </summary>
    public static async Task<string> WriteFileAsync(
        string path,
        string content,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string full = Path.GetFullPath(path);
        string? directory = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(full, content, cancellationToken).ConfigureAwait(false);
        return full;
    }

    /// <summary>
    /// Writes a set of generated files, optionally under <paramref name="baseDirectory"/>.
    /// Returns the absolute paths written, in order.
    /// </summary>
    public static async Task<IReadOnlyList<string>> WriteFilesAsync(
        IEnumerable<OutputFile> files,
        string? baseDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(files);

        var written = new List<string>();
        foreach (OutputFile file in files)
        {
            string target = baseDirectory is null || Path.IsPathRooted(file.Path)
                ? file.Path
                : Path.Combine(baseDirectory, file.Path);

            written.Add(await WriteFileAsync(target, file.Content, cancellationToken).ConfigureAwait(false));
        }

        return written;
    }

    /// <summary>
    /// Prints <paramref name="content"/> to the terminal. An optional <paramref name="title"/>
    /// is shown as a rule above the body. Content is rendered literally (no markup parsing) so
    /// arbitrary model output cannot corrupt the display.
    /// </summary>
    public static void Print(string content, string? title = null)
    {
        ArgumentNullException.ThrowIfNull(content);

        if (!string.IsNullOrWhiteSpace(title))
        {
            AnsiConsole.Write(new Rule(Markup.Escape(title)).LeftJustified());
        }

        // WriteLine(string) writes the literal string without interpreting Spectre markup.
        AnsiConsole.WriteLine(content.TrimEnd());
    }

    // Yields the raw text plus a fence-stripped / brace-trimmed variant, so a fenced or
    // prose-wrapped JSON payload still parses on the second candidate.
    private static IEnumerable<string> JsonCandidates(string text)
    {
        string trimmed = text.Trim();
        yield return trimmed;

        string stripped = StripCodeFence(trimmed);
        if (!string.Equals(stripped, trimmed, StringComparison.Ordinal))
        {
            yield return stripped;
        }

        string carved = CarveOutermost(stripped);
        if (carved.Length > 0 && !string.Equals(carved, stripped, StringComparison.Ordinal))
        {
            yield return carved;
        }
    }

    private static string StripCodeFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        int firstNewline = text.IndexOf('\n');
        if (firstNewline < 0)
        {
            return text;
        }

        // Drop the opening fence line (which may carry a language tag such as ```json).
        string withoutOpen = text[(firstNewline + 1)..];
        int closingFence = withoutOpen.LastIndexOf("```", StringComparison.Ordinal);
        return (closingFence >= 0 ? withoutOpen[..closingFence] : withoutOpen).Trim();
    }

    // Returns the substring from the first opening bracket/brace to its matching close, so
    // leading or trailing prose around a JSON object or array is discarded.
    private static string CarveOutermost(string text)
    {
        int objectStart = text.IndexOf('{');
        int arrayStart = text.IndexOf('[');

        int start = (objectStart, arrayStart) switch
        {
            (< 0, < 0) => -1,
            (< 0, _) => arrayStart,
            (_, < 0) => objectStart,
            _ => Math.Min(objectStart, arrayStart),
        };

        if (start < 0)
        {
            return string.Empty;
        }

        char open = text[start];
        char close = open == '{' ? '}' : ']';
        int end = text.LastIndexOf(close);
        return end > start ? text[start..(end + 1)] : string.Empty;
    }
}

/// <summary>A file to write as part of post-processing: a relative-or-absolute path and its body.</summary>
public sealed record OutputFile(string Path, string Content);

/// <summary>Raised when the model fails to produce valid JSON even after the corrective retry.</summary>
public sealed class StructuredOutputException : Exception
{
    public StructuredOutputException(string message, string rawResponse) : base(message)
    {
        RawResponse = rawResponse;
    }

    /// <summary>The last raw model response, kept for diagnostics and display.</summary>
    public string RawResponse { get; }
}
