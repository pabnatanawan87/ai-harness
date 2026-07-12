using System.Text.Json;

using AiHarness.Rendering;

using Xunit;

namespace AiHarness.Tests;

public sealed class PostProcessorTests
{
    [Fact]
    public async Task GetStructuredAsync_ParsesValidJsonOnFirstTry()
    {
        var client = new FakeChatClient("{\"ok\": true}");
        var processor = new PostProcessor(client);

        JsonElement element = await processor.GetStructuredAsync("give me json");

        Assert.True(element.GetProperty("ok").GetBoolean());
        Assert.Single(client.Calls);
    }

    [Fact]
    public async Task GetStructuredAsync_RetriesOnceThenSucceeds()
    {
        // First reply is prose (invalid), second is clean JSON; the retry policy recovers.
        var client = new FakeChatClient("sorry, here is your data", "{\"value\": 42}");
        var processor = new PostProcessor(client);

        JsonElement element = await processor.GetStructuredAsync("give me json");

        Assert.Equal(42, element.GetProperty("value").GetInt32());
        Assert.Equal(2, client.Calls.Count);
    }

    [Fact]
    public async Task GetStructuredAsync_ThrowsAfterTwoInvalidReplies()
    {
        var client = new FakeChatClient("not json", "still not json");
        var processor = new PostProcessor(client);

        StructuredOutputException ex = await Assert.ThrowsAsync<StructuredOutputException>(
            () => processor.GetStructuredAsync("give me json"));

        Assert.Equal("still not json", ex.RawResponse);
    }

    [Fact]
    public void TryParse_StripsCodeFenceAndSurroundingProse()
    {
        const string fenced = "Here you go:\n```json\n{\"a\": 1}\n```\nthanks";

        bool ok = PostProcessor.TryParse(fenced, out JsonElement element, out string error);

        Assert.True(ok, error);
        Assert.Equal(1, element.GetProperty("a").GetInt32());
    }

    [Fact]
    public async Task WriteFileAsync_CreatesParentDirectoriesAndReturnsAbsolutePath()
    {
        string target = Path.Combine(
            Path.GetTempPath(),
            "aiharness-tests-" + Guid.NewGuid().ToString("N"),
            "nested",
            "out.md");

        try
        {
            string written = await PostProcessor.WriteFileAsync(target, "hello");

            Assert.True(Path.IsPathRooted(written));
            Assert.Equal("hello", await File.ReadAllTextAsync(written));
        }
        finally
        {
            string? dir = Path.GetDirectoryName(Path.GetDirectoryName(target));
            if (dir is not null && Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
