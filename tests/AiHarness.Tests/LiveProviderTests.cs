using AiHarness;
using AiHarness.Providers;

using Microsoft.Extensions.AI;

using Xunit;

namespace AiHarness.Tests;

/// <summary>
/// A single live integration test that actually calls the configured provider. It is gated
/// on the AIHARNESS_LIVE environment variable so the normal test run (and CI) spends no
/// tokens: unless AIHARNESS_LIVE=1, the test returns immediately. To run it, set your
/// provider env vars (see .env.example) plus AIHARNESS_LIVE=1, then:
///   dotnet test --filter Category=Live
/// </summary>
[Trait("Category", "Live")]
public sealed class LiveProviderTests
{
    [Fact]
    public async Task ConfiguredProvider_AnswersAHelloPrompt()
    {
        if (Environment.GetEnvironmentVariable("AIHARNESS_LIVE") != "1")
        {
            // Not a live run: skip without failing. This keeps the default suite offline.
            return;
        }

        DotEnv.Load();
        HarnessConfig config = HarnessConfig.FromEnvironment();
        using IChatClient client = ProviderFactory.Create(config);

        ChatResponse response = await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "Reply with the single word: pong.") });

        Assert.False(string.IsNullOrWhiteSpace(response.Text));
    }
}
