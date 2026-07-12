using System.ComponentModel;
using System.Diagnostics;
using System.Text;

namespace AiHarness.Context;

/// <summary>
/// A single labeled block of gathered context, ready to be injected into a prompt.
/// The <see cref="Label"/> is a short human-readable heading (for example
/// "files: src/Program.cs") and <see cref="Text"/> is the body.
/// </summary>
public sealed record ContextBlock(string Label, string Text);

/// <summary>
/// A request to run one built-in gatherer. <see cref="Kind"/> selects the gatherer
/// (files | diff | ripgrep | repo_map | stdin) and <see cref="Argument"/> is its
/// optional parameter (a path list, a search pattern, or "staged" for a diff). This
/// mirrors the data-defined "context:" entries in a skill.yaml so a skill loader can
/// map manifest lines straight onto these specs.
/// </summary>
public sealed record ContextSpec(string Kind, string? Argument = null);

/// <summary>
/// The built-in context gatherers: files, git diff, ripgrep, repo_map, and stdin.
///
/// Each gatherer turns some slice of the working tree (or piped input) into a plain-text
/// <see cref="ContextBlock"/>. Gatherers are deliberately defensive: a missing tool
/// (git or ripgrep not installed) or a failed command yields an explanatory block rather
/// than throwing, so a run degrades gracefully instead of crashing. Output is capped so a
/// giant file or a broad search cannot blow up the prompt.
/// </summary>
public static class ContextGatherer
{
    /// <summary>Maximum bytes read from any single file before the body is truncated.</summary>
    public const int MaxFileBytes = 200_000;

    /// <summary>Maximum characters kept from a single command's output before truncation.</summary>
    public const int MaxProcessOutputChars = 100_000;

    /// <summary>Maximum number of paths listed by the repo_map gatherer.</summary>
    public const int MaxRepoMapEntries = 2_000;

    private static readonly string[] IgnoredDirectories =
    {
        ".git", "bin", "obj", "node_modules", ".vs", ".idea", "packages", "dist", "out",
    };

    /// <summary>
    /// Runs every spec in order and returns the resulting blocks. Specs that produce no
    /// content (for example an empty diff) are skipped. <paramref name="workingDirectory"/>
    /// defaults to the current directory and is the root for file, git, and ripgrep work.
    /// </summary>
    public static async Task<IReadOnlyList<ContextBlock>> GatherAsync(
        IEnumerable<ContextSpec> specs,
        string? workingDirectory = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(specs);

        string root = workingDirectory ?? Directory.GetCurrentDirectory();
        var blocks = new List<ContextBlock>();

        foreach (ContextSpec spec in specs)
        {
            ContextBlock? block = await GatherOneAsync(spec, root, cancellationToken).ConfigureAwait(false);
            if (block is not null && block.Text.Length > 0)
            {
                blocks.Add(block);
            }
        }

        return blocks;
    }

    /// <summary>Dispatches a single spec to the matching gatherer.</summary>
    public static Task<ContextBlock> GatherOneAsync(
        ContextSpec spec,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(spec);

        string kind = spec.Kind.Trim().ToLowerInvariant();
        string argument = spec.Argument?.Trim() ?? string.Empty;

        return kind switch
        {
            "files" or "file" => FilesAsync(argument, workingDirectory, cancellationToken),
            "diff" or "git" or "git_diff" or "gitdiff" =>
                GitDiffAsync(IsStaged(argument), workingDirectory, cancellationToken),
            "ripgrep" or "rg" or "grep" => RipgrepAsync(argument, workingDirectory, cancellationToken),
            "repo_map" or "repomap" or "map" => RepoMapAsync(workingDirectory, cancellationToken),
            "stdin" or "-" => StdinAsync(cancellationToken),
            _ => throw new ArgumentException(
                $"Unknown context gatherer '{spec.Kind}'. Valid kinds: files, diff, ripgrep, repo_map, stdin.",
                nameof(spec)),
        };
    }

    /// <summary>
    /// Reads one or more files named in <paramref name="paths"/> (comma- or newline-separated,
    /// simple '*' and '**' globs allowed) and concatenates them with per-file headers.
    /// </summary>
    public static async Task<ContextBlock> FilesAsync(
        string paths,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> resolved = ResolveFilePaths(paths, workingDirectory);
        if (resolved.Count == 0)
        {
            return new ContextBlock(
                $"files: {paths}",
                $"(no files matched '{paths}')");
        }

        var body = new StringBuilder();
        foreach (string path in resolved)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string relative = ToRelative(path, workingDirectory);
            body.Append("### ").Append(relative).Append('\n');

            try
            {
                string content = await ReadCappedAsync(path, cancellationToken).ConfigureAwait(false);
                body.Append("```\n").Append(content);
                if (content.Length == 0 || content[^1] != '\n')
                {
                    body.Append('\n');
                }

                body.Append("```\n\n");
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                body.Append("(could not read: ").Append(ex.Message).Append(")\n\n");
            }
        }

        string label = resolved.Count == 1
            ? $"files: {ToRelative(resolved[0], workingDirectory)}"
            : $"files: {resolved.Count} file(s) matching '{paths}'";

        return new ContextBlock(label, body.ToString().TrimEnd() + "\n");
    }

    /// <summary>
    /// Runs "git diff" (or "git diff --staged" when <paramref name="staged"/> is true) in the
    /// working directory. Returns a note instead of throwing when git is unavailable.
    /// </summary>
    public static async Task<ContextBlock> GitDiffAsync(
        bool staged,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        var arguments = new List<string> { "diff" };
        if (staged)
        {
            arguments.Add("--staged");
        }

        string label = staged ? "git diff --staged" : "git diff";
        ProcessResult result = await RunProcessAsync("git", arguments, workingDirectory, null, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Started)
        {
            return new ContextBlock(label, "(git is not installed or not on PATH)");
        }

        if (result.ExitCode != 0)
        {
            string detail = string.IsNullOrWhiteSpace(result.StdErr) ? "git diff failed" : result.StdErr.Trim();
            return new ContextBlock(label, $"(git diff failed: {detail})");
        }

        string diff = result.StdOut.Trim();
        return new ContextBlock(label, diff.Length == 0 ? "(no changes)" : diff);
    }

    /// <summary>
    /// Searches the working tree with ripgrep for <paramref name="pattern"/> and returns the
    /// matching lines with file names and line numbers.
    /// </summary>
    public static async Task<ContextBlock> RipgrepAsync(
        string pattern,
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        string label = $"ripgrep: {pattern}";
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return new ContextBlock(label, "(no search pattern supplied)");
        }

        // --line-number and --heading group hits by file; --color never keeps output clean.
        var arguments = new List<string>
        {
            "--line-number", "--heading", "--color", "never", "--max-columns", "300", pattern,
        };

        ProcessResult result = await RunProcessAsync("rg", arguments, workingDirectory, null, cancellationToken)
            .ConfigureAwait(false);

        if (!result.Started)
        {
            return new ContextBlock(label, "(ripgrep 'rg' is not installed or not on PATH)");
        }

        // ripgrep exits 1 when there are simply no matches; that is not an error here.
        if (result.ExitCode > 1)
        {
            string detail = string.IsNullOrWhiteSpace(result.StdErr) ? "ripgrep failed" : result.StdErr.Trim();
            return new ContextBlock(label, $"(ripgrep failed: {detail})");
        }

        string hits = result.StdOut.Trim();
        return new ContextBlock(label, hits.Length == 0 ? "(no matches)" : hits);
    }

    /// <summary>
    /// Produces a compact list of the files in the repository. Prefers "git ls-files" so
    /// ignored paths are skipped; falls back to a filtered directory walk when git is
    /// unavailable or the directory is not a git repo.
    /// </summary>
    public static async Task<ContextBlock> RepoMapAsync(
        string workingDirectory,
        CancellationToken cancellationToken = default)
    {
        const string label = "repo_map";

        ProcessResult git = await RunProcessAsync(
            "git",
            new[] { "ls-files" },
            workingDirectory,
            null,
            cancellationToken).ConfigureAwait(false);

        IEnumerable<string> paths;
        if (git.Started && git.ExitCode == 0 && git.StdOut.Trim().Length > 0)
        {
            paths = git.StdOut
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
        else
        {
            paths = WalkFiles(workingDirectory, cancellationToken)
                .Select(p => ToRelative(p, workingDirectory).Replace('\\', '/'));
        }

        var ordered = paths
            .Where(p => p.Length > 0)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var body = new StringBuilder();
        int shown = Math.Min(ordered.Count, MaxRepoMapEntries);
        for (int i = 0; i < shown; i++)
        {
            body.Append(ordered[i]).Append('\n');
        }

        if (ordered.Count > shown)
        {
            body.Append("... (").Append(ordered.Count - shown).Append(" more file(s) omitted)\n");
        }

        if (ordered.Count == 0)
        {
            return new ContextBlock(label, "(no files found)");
        }

        return new ContextBlock($"{label}: {ordered.Count} file(s)", body.ToString().TrimEnd() + "\n");
    }

    /// <summary>Reads all of standard input as a single context block (for piped data).</summary>
    public static async Task<ContextBlock> StdinAsync(CancellationToken cancellationToken = default)
    {
        const string label = "stdin";

        if (Console.IsInputRedirected)
        {
            string text = await Console.In.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
            text = Cap(text, MaxProcessOutputChars);
            return new ContextBlock(label, text.Trim().Length == 0 ? "(empty)" : text.TrimEnd());
        }

        return new ContextBlock(label, "(no piped input)");
    }

    private static bool IsStaged(string argument) =>
        argument.Equals("staged", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("cached", StringComparison.OrdinalIgnoreCase)
        || argument.Equals("--staged", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Expands a comma/newline-separated path list into concrete file paths. Entries with
    /// '*' or '?' are treated as globs; '**' means recurse into subdirectories.
    /// </summary>
    private static IReadOnlyList<string> ResolveFilePaths(string paths, string workingDirectory)
    {
        if (string.IsNullOrWhiteSpace(paths))
        {
            return Array.Empty<string>();
        }

        var results = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string raw in paths.Split(new[] { ',', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            foreach (string match in ExpandEntry(raw, workingDirectory))
            {
                if (seen.Add(match))
                {
                    results.Add(match);
                }
            }
        }

        return results;
    }

    private static IEnumerable<string> ExpandEntry(string entry, string workingDirectory)
    {
        string normalized = entry.Replace('\\', '/');
        bool rooted = Path.IsPathRooted(normalized);
        string combined = rooted ? normalized : Path.Combine(workingDirectory, normalized);

        if (!normalized.Contains('*') && !normalized.Contains('?'))
        {
            string full = Path.GetFullPath(combined);
            return File.Exists(full) ? new[] { full } : Array.Empty<string>();
        }

        // Split into the fixed base directory and the wildcard pattern.
        bool recursive = normalized.Contains("**");
        string cleaned = normalized.Replace("**/", string.Empty).Replace("**", string.Empty);
        string baseRoot = rooted ? string.Empty : workingDirectory;

        string dirPart = Path.GetDirectoryName(cleaned) ?? string.Empty;
        string pattern = Path.GetFileName(cleaned);
        if (pattern.Length == 0)
        {
            pattern = "*";
        }

        string searchDir = Path.GetFullPath(
            dirPart.Length == 0 ? baseRoot : (rooted ? dirPart : Path.Combine(baseRoot, dirPart)));

        if (!Directory.Exists(searchDir))
        {
            return Array.Empty<string>();
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        try
        {
            return Directory.EnumerateFiles(searchDir, pattern, option)
                .Where(p => !IsInIgnoredDirectory(p, workingDirectory))
                .Select(Path.GetFullPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> WalkFiles(string root, CancellationToken cancellationToken)
    {
        var pending = new Stack<string>();
        pending.Push(root);

        while (pending.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string current = pending.Pop();

            string[] subdirectories;
            string[] files;
            try
            {
                subdirectories = Directory.GetDirectories(current);
                files = Directory.GetFiles(current);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                continue;
            }

            foreach (string directory in subdirectories)
            {
                string name = Path.GetFileName(directory);
                if (!IgnoredDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    pending.Push(directory);
                }
            }

            foreach (string file in files)
            {
                yield return file;
            }
        }
    }

    private static bool IsInIgnoredDirectory(string path, string workingDirectory)
    {
        string relative = ToRelative(path, workingDirectory).Replace('\\', '/');
        foreach (string segment in relative.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (IgnoredDirectories.Contains(segment, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static async Task<string> ReadCappedAsync(string path, CancellationToken cancellationToken)
    {
        var info = new FileInfo(path);
        if (info.Length <= MaxFileBytes)
        {
            return await File.ReadAllTextAsync(path, cancellationToken).ConfigureAwait(false);
        }

        // Read only the leading window of an oversized file and note the truncation.
        var buffer = new byte[MaxFileBytes];
        int read;
        await using (FileStream stream = File.OpenRead(path))
        {
            read = await stream.ReadAsync(buffer.AsMemory(0, MaxFileBytes), cancellationToken).ConfigureAwait(false);
        }

        string head = Encoding.UTF8.GetString(buffer, 0, read);
        return head + $"\n... (truncated; file is {info.Length} bytes, showing first {MaxFileBytes})\n";
    }

    private static string ToRelative(string path, string basePath)
    {
        try
        {
            string relative = Path.GetRelativePath(basePath, path);
            return relative.StartsWith("..", StringComparison.Ordinal) ? path : relative;
        }
        catch (ArgumentException)
        {
            return path;
        }
    }

    private static string Cap(string text, int maxChars)
    {
        if (text.Length <= maxChars)
        {
            return text;
        }

        return text[..maxChars] + $"\n... (truncated at {maxChars} characters)\n";
    }

    private static async Task<ProcessResult> RunProcessAsync(
        string fileName,
        IEnumerable<string> arguments,
        string workingDirectory,
        string? standardInput,
        CancellationToken cancellationToken)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = standardInput is not null,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        using var process = new Process { StartInfo = startInfo };

        try
        {
            if (!process.Start())
            {
                return ProcessResult.NotStarted;
            }
        }
        catch (Win32Exception)
        {
            // Executable not found on PATH.
            return ProcessResult.NotStarted;
        }

        Task<string> stdOutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> stdErrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        if (standardInput is not null)
        {
            await process.StandardInput.WriteAsync(standardInput).ConfigureAwait(false);
            process.StandardInput.Close();
        }

        try
        {
            await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            throw;
        }

        string stdOut = Cap(await stdOutTask.ConfigureAwait(false), MaxProcessOutputChars);
        string stdErr = await stdErrTask.ConfigureAwait(false);
        return new ProcessResult(true, process.ExitCode, stdOut, stdErr);
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or Win32Exception or NotSupportedException)
        {
            // Best effort: the process may have already exited.
        }
    }

    private readonly record struct ProcessResult(bool Started, int ExitCode, string StdOut, string StdErr)
    {
        public static ProcessResult NotStarted { get; } = new(false, -1, string.Empty, string.Empty);
    }
}
