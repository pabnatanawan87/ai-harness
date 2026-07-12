using System.Runtime.CompilerServices;

using Microsoft.Extensions.AI;

namespace AiHarness.Tests;

/// <summary>
/// A test double for <see cref="IChatClient"/> that returns a canned response without any
/// network call or token spend (DESIGN section 8). It records the messages and options it
/// was handed so a test can assert what the pipeline actually sent to the model, and it can
/// return a different reply on each call so the one-retry-on-invalid-JSON path is testable.
/// </summary>
public sealed class FakeChatClient : IChatClient
{
    private readonly Queue<string> _responses;

    /// <summary>Creates a client that returns each response in turn (the last one repeats).</summary>
    public FakeChatClient(params string[] responses)
    {
        if (responses is null || responses.Length == 0)
        {
            responses = new[] { string.Empty };
        }

        _responses = new Queue<string>(responses);
    }

    /// <summary>Every message list the client was asked to complete, in call order.</summary>
    public List<IReadOnlyList<ChatMessage>> Calls { get; } = new();

    /// <summary>The <see cref="ChatOptions"/> handed to the most recent call, if any.</summary>
    public ChatOptions? LastOptions { get; private set; }

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        Calls.Add(messages.ToList());
        LastOptions = options;

        string text = _responses.Count == 1 ? _responses.Peek() : _responses.Dequeue();
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, text));
        return Task.FromResult(response);
    }

    public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ChatResponse response = await GetResponseAsync(messages, options, cancellationToken)
            .ConfigureAwait(false);
        yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
    }

    public object? GetService(Type serviceType, object? serviceKey = null) => null;

    public void Dispose()
    {
        // Nothing to release.
    }
}
