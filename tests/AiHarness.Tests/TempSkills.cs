namespace AiHarness.Tests;

/// <summary>
/// A disposable temporary skills directory for hermetic loader tests. Each instance owns a
/// unique folder under the system temp path and cleans it up on dispose, so tests never
/// depend on the repository's real ./skills folder or leave anything behind.
/// </summary>
public sealed class TempSkills : IDisposable
{
    public TempSkills()
    {
        Root = Path.Combine(Path.GetTempPath(), "aiharness-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    /// <summary>Absolute path of the temporary "skills" root directory.</summary>
    public string Root { get; }

    /// <summary>
    /// Writes a skill folder with the given manifest text and prompt files. Each prompt is a
    /// (relative path, content) pair, e.g. ("prompts/main.md", "Hello {{name}}").
    /// </summary>
    public string WriteSkill(string name, string manifest, params (string Path, string Content)[] prompts)
    {
        string folder = Path.Combine(Root, name);
        Directory.CreateDirectory(folder);
        File.WriteAllText(Path.Combine(folder, "skill.yaml"), manifest);

        foreach ((string path, string content) in prompts)
        {
            string full = Path.Combine(folder, path.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(full)!);
            File.WriteAllText(full, content);
        }

        return folder;
    }

    /// <summary>Creates a bare directory (no skill.yaml) to prove it is ignored by LoadAll.</summary>
    public void WriteEmptyFolder(string name) =>
        Directory.CreateDirectory(Path.Combine(Root, name));

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best effort cleanup; a locked temp file should not fail the test run.
        }
    }
}
