using System.ClientModel;

using Microsoft.Extensions.AI;

using OpenAI;

namespace AiHarness.Providers;

/// <summary>
/// The single vendor seam of ai-harness.
///
/// This is the ONLY file in the project that references a vendor SDK package
/// (Microsoft.Extensions.AI.OpenAI / OpenAI). Everything else in the codebase depends
/// only on <see cref="IChatClient"/> from Microsoft.Extensions.AI. To add or swap a
/// backend, change this file and nothing else. Keep it that way - it is the portability
/// guarantee the whole tool rests on.
/// </summary>
public static class ProviderFactory
{
    /// <summary>
    /// Builds an <see cref="IChatClient"/> for the provider named in <paramref name="config"/>,
    /// reading the raw credentials directly from the environment. Throws a clear
    /// <see cref="ProviderConfigurationException"/> when required configuration is missing
    /// or when a provider is recognized but not yet wired.
    /// </summary>
    public static IChatClient Create(HarnessConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);

        return config.Provider switch
        {
            "openai" => CreateOpenAi(config.Model),
            "local" => CreateLocal(config.Model),
            "azure" => throw NotYetWired("azure", "AZURE_OPENAI_* + the Azure.AI.OpenAI package"),
            "anthropic" => throw NotYetWired("anthropic", "ANTHROPIC_API_KEY + an Anthropic adapter"),
            _ => throw new ProviderConfigurationException(
                $"Unknown provider '{config.Provider}'. Set {HarnessConfig.ProviderVar} to one of: openai, azure, anthropic, local."),
        };
    }

    private static IChatClient CreateOpenAi(string model)
    {
        string key = Require("OPENAI_API_KEY", "openai");
        string modelId = RequireModel(model);

        return new OpenAIClient(new ApiKeyCredential(key))
            .GetChatClient(modelId)
            .AsIChatClient();
    }

    private static IChatClient CreateLocal(string model)
    {
        string baseUrl = Require("LOCAL_BASE_URL", "local");
        // Local OpenAI-compatible servers usually ignore the key; a placeholder is fine.
        string key = Environment.GetEnvironmentVariable("LOCAL_API_KEY") ?? "local";
        string modelId = RequireModel(model);

        var options = new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };
        return new OpenAIClient(new ApiKeyCredential(key), options)
            .GetChatClient(modelId)
            .AsIChatClient();
    }

    private static string Require(string envVar, string provider)
    {
        string? value = Environment.GetEnvironmentVariable(envVar);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProviderConfigurationException(
                $"Provider '{provider}' requires the {envVar} environment variable. Set it in your .env file.");
        }

        return value;
    }

    private static string RequireModel(string model)
    {
        if (string.IsNullOrWhiteSpace(model))
        {
            throw new ProviderConfigurationException(
                $"No model configured. Set {HarnessConfig.ModelVar} in your .env file.");
        }

        return model;
    }

    private static ProviderConfigurationException NotYetWired(string provider, string needs) =>
        new($"Provider '{provider}' is recognized but not yet wired in this milestone (needs {needs}). Use 'openai' or 'local' for now.");
}

/// <summary>Raised when provider configuration is missing, invalid, or not yet available.</summary>
public sealed class ProviderConfigurationException : Exception
{
    public ProviderConfigurationException(string message) : base(message)
    {
    }
}
