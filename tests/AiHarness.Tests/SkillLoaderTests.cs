using AiHarness.Skills;

using Xunit;

namespace AiHarness.Tests;

public sealed class SkillLoaderTests
{
    private const string FullManifest = """
        name: rca
        description: Decompose a symptom into verified root-cause hypotheses
        inputs: [symptom]
        context:
          - repo_map
          - ripgrep: "{{symptom}}"
        steps:
          - id: decompose
            prompt: prompts/decompose.md
            produces: hypotheses
          - id: verify
            foreach: hypotheses
            as: hypothesis
            prompt: prompts/verify.md
            produces: verdicts
        output: markdown
        """;

    [Fact]
    public void Load_ParsesManifestContextStepsAndTemplates()
    {
        using var temp = new TempSkills();
        temp.WriteSkill(
            "rca",
            FullManifest,
            ("prompts/decompose.md", "Decompose {{symptom}}"),
            ("prompts/verify.md", "Verify {{hypothesis}}"));

        var loader = new SkillLoader(new[] { temp.Root });
        Skill skill = loader.Load("rca");

        Assert.Equal("rca", skill.Name);
        Assert.Equal("Decompose a symptom into verified root-cause hypotheses", skill.Description);
        Assert.Equal(new[] { "symptom" }, skill.Inputs);
        Assert.Equal("markdown", skill.Output);

        // Both context forms: the bare name and the single-key mapping with an argument.
        Assert.Equal(2, skill.Context.Count);
        Assert.Equal("repo_map", skill.Context[0].Name);
        Assert.False(skill.Context[0].HasArgument);
        Assert.Equal("ripgrep", skill.Context[1].Name);
        Assert.True(skill.Context[1].HasArgument);
        Assert.Equal("{{symptom}}", skill.Context[1].Argument);

        // Single-shot step plus a foreach step; templates read eagerly from disk.
        Assert.Equal(2, skill.Steps.Count);
        Assert.False(skill.Steps[0].IsForEach);
        Assert.Equal("Decompose {{symptom}}", skill.Steps[0].PromptTemplate);
        Assert.True(skill.Steps[1].IsForEach);
        Assert.Equal("hypotheses", skill.Steps[1].ForEach);
    }

    [Fact]
    public void Load_AppliesDefaultsOnMinimalManifest()
    {
        using var temp = new TempSkills();
        temp.WriteSkill(
            "mini",
            "name: mini\nsteps:\n  - prompt: prompts/main.md\n",
            ("prompts/main.md", "Body"));

        var loader = new SkillLoader(new[] { temp.Root });
        Skill skill = loader.Load("mini");

        Assert.Equal(Skill.DefaultOutput, skill.Output);
        Assert.Empty(skill.Inputs);
        Assert.Empty(skill.Context);
        Assert.Equal(string.Empty, skill.Description);
    }

    [Fact]
    public void TryLoad_ReturnsFalseForMissingSkill()
    {
        using var temp = new TempSkills();
        var loader = new SkillLoader(new[] { temp.Root });

        Assert.False(loader.TryLoad("nope", out Skill? skill));
        Assert.Null(skill);
    }

    [Fact]
    public void Load_ThrowsForMissingSkill()
    {
        using var temp = new TempSkills();
        var loader = new SkillLoader(new[] { temp.Root });

        Assert.Throws<SkillLoadException>(() => loader.Load("ghost"));
    }

    [Fact]
    public void Load_ThrowsWhenPromptFileMissing()
    {
        using var temp = new TempSkills();
        temp.WriteSkill("broken", "name: broken\nsteps:\n  - prompt: prompts/gone.md\n");

        var loader = new SkillLoader(new[] { temp.Root });

        SkillLoadException ex = Assert.Throws<SkillLoadException>(() => loader.Load("broken"));
        Assert.Contains("gone.md", ex.Message);
    }

    [Fact]
    public void Load_ThrowsWhenNoSteps()
    {
        using var temp = new TempSkills();
        temp.WriteSkill("nosteps", "name: nosteps\ndescription: no steps here\n");

        var loader = new SkillLoader(new[] { temp.Root });

        Assert.Throws<SkillLoadException>(() => loader.Load("nosteps"));
    }

    [Fact]
    public void LoadAll_SortsDedupesAndIgnoresNonSkillFolders()
    {
        using var high = new TempSkills();
        using var low = new TempSkills();

        // Same name in both roots; the first (high-precedence) root must win.
        high.WriteSkill("dup", "name: dup\ndescription: from-high\nsteps:\n  - prompt: p.md\n", ("p.md", "x"));
        low.WriteSkill("dup", "name: dup\ndescription: from-low\nsteps:\n  - prompt: p.md\n", ("p.md", "x"));

        high.WriteSkill("beta", "name: beta\nsteps:\n  - prompt: p.md\n", ("p.md", "x"));
        low.WriteSkill("alpha", "name: alpha\nsteps:\n  - prompt: p.md\n", ("p.md", "x"));
        low.WriteEmptyFolder("not-a-skill");

        var loader = new SkillLoader(new[] { high.Root, low.Root });
        IReadOnlyList<Skill> all = loader.LoadAll();

        Assert.Equal(new[] { "alpha", "beta", "dup" }, all.Select(s => s.Name).ToArray());
        Assert.Equal("from-high", all.Single(s => s.Name == "dup").Description);
    }
}
