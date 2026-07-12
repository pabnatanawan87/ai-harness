namespace AiHarness;

/// <summary>
/// Minimal ".env" loader. No dependency, no magic: read KEY=VALUE lines from a local
/// .env file and set them as process environment variables (only if not already set,
/// so a real environment variable always wins over the file). Secrets never touch code
/// or source control; they live in the gitignored .env or the ambient environment.
/// </summary>
public static class DotEnv
{
    /// <summary>
    /// Loads a .env file by walking up from <paramref name="startDirectory"/> (default:
    /// current directory) until one is found or the filesystem root is reached. Missing
    /// file is not an error - the process environment is used as-is.
    /// </summary>
    public static void Load(string? startDirectory = null)
    {
        string? path = FindEnvFile(startDirectory ?? Directory.GetCurrentDirectory());
        if (path is null)
        {
            return;
        }

        foreach (string rawLine in File.ReadAllLines(path))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line[0] == '#')
            {
                continue;
            }

            int eq = line.IndexOf('=');
            if (eq <= 0)
            {
                continue;
            }

            string key = line[..eq].Trim();
            string value = line[(eq + 1)..].Trim();

            // Strip one layer of surrounding quotes if present.
            if (value.Length >= 2 &&
                ((value[0] == '"' && value[^1] == '"') || (value[0] == '\'' && value[^1] == '\'')))
            {
                value = value[1..^1];
            }

            // Ambient environment wins; do not overwrite a value already set.
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            {
                Environment.SetEnvironmentVariable(key, value);
            }
        }
    }

    private static string? FindEnvFile(string startDirectory)
    {
        DirectoryInfo? dir = new(startDirectory);
        while (dir is not null)
        {
            string candidate = Path.Combine(dir.FullName, ".env");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return null;
    }
}
