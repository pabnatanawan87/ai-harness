using AiHarness.Cli;
using AiHarness.Cli.Abstractions;
using AiHarness.Composition;
using AiHarness.Skills;

using Spectre.Console.Testing;

using Xunit;

namespace AiHarness.Tests;

/// <summary>
/// End-to-end orchestration tests for "ai run": the composition adapters plus
/// <see cref="RunPipeline"/>, driven by a <see cref="FakeChatClient"/> so no real model is
/// called. This proves the concrete modules line up behind the CLI seams.
/// </summary>
public sealed class PipelineTests
{
    private const string EchoManifest = """
        name: echo
        description: Echo the topic back
        inputs: [topic]
        steps:
          - prompt: prompts/main.md
        output: text
        """;

    [Fact]
    public async Task Run_RendersInputsGetsModelReplyAndPrintsIt()
    {
        using var temp = new TempSkills();
        temp.WriteSkill("echo", EchoManifest, ("prompts/main.md", "Topic is {{topic}}"));

        var loader = new SkillLoader(new[] { temp.Root });
        CliServices services = HarnessComposition.Build(loader);
        var client = new FakeChatClient("MODEL_REPLY");
        var console = new TestConsole();

        var pipeline = new RunPipeline(services, console, () => client);
        int exitCode = await pipeline.RunAsync(
            "echo",
            new RunOptions(FilePath: null, IncludeDiff: false, Input: "login-bug", OutPath: null),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.Contains("MODEL_REPLY", console.Output);

        // The rendered prompt reached the model with the input substituted in.
        Assert.Single(client.Calls);
        Assert.Contains("Topic is login-bug", client.Calls[0][0].Text);
    }

    [Fact]
    public async Task Run_WritesToOutPathWhenGiven()
    {
        using var temp = new TempSkills();
        temp.WriteSkill("echo", EchoManifest, ("prompts/main.md", "Topic is {{topic}}"));

        var loader = new SkillLoader(new[] { temp.Root });
        CliServices services = HarnessComposition.Build(loader);
        var client = new FakeChatClient("FILE_BODY");
        var console = new TestConsole();
        string outPath = Path.Combine(temp.Root, "result.txt");

        var pipeline = new RunPipeline(services, console, () => client);
        int exitCode = await pipeline.RunAsync(
            "echo",
            new RunOptions(FilePath: null, IncludeDiff: false, Input: "x", OutPath: outPath),
            CancellationToken.None);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(outPath));
        Assert.Contains("FILE_BODY", await File.ReadAllTextAsync(outPath));
    }

    [Fact]
    public async Task Run_ReturnsNonZeroForUnknownSkill()
    {
        using var temp = new TempSkills();
        var loader = new SkillLoader(new[] { temp.Root });
        CliServices services = HarnessComposition.Build(loader);
        var console = new TestConsole();

        var pipeline = new RunPipeline(services, console, () => new FakeChatClient("unused"));
        int exitCode = await pipeline.RunAsync(
            "ghost",
            new RunOptions(null, false, null, null),
            CancellationToken.None);

        Assert.Equal(1, exitCode);
    }

    [Fact]
    public void Catalog_ListsAndResolvesSkillsThroughTheAdapter()
    {
        using var temp = new TempSkills();
        temp.WriteSkill("echo", EchoManifest, ("prompts/main.md", "Topic is {{topic}}"));

        var loader = new SkillLoader(new[] { temp.Root });
        CliServices services = HarnessComposition.Build(loader);

        Assert.Single(services.SkillCatalog.List());
        SkillInfo info = services.SkillCatalog.Get("echo");
        Assert.Equal("echo", info.Name);
        Assert.Equal(new[] { "topic" }, info.Inputs);
        Assert.Throws<SkillNotFoundException>(() => services.SkillCatalog.Get("missing"));
    }
}
