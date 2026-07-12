namespace AiHarness;

/// <summary>
/// The resolved, display-safe view of ai-harness configuration. It exposes the chosen
/// provider and model plus whether the relevant credentials are PRESENT - never the
/// secret values themselves. Provider construction reads raw keys straight from the
/// environment (see <see cref="AiHarness.Providers.ProviderFactory"/>); this type is the
/// safe surface used for "ai config" output and for choosing a backend.
/// </summary>
public sealed class HarnessConfig
{
    /// <summary>Environment variable holding the provider id (openai|azure|anthropic|local).</summary>
    public const string ProviderVar = "AIHARNESS_PROVIDER";

    /// <summary>Environment variable holding the model / deployment id.</summary>
    public const string ModelVar = "AIHARNESS_MODEL";

    private HarnessConfig(string provider, string model, IReadOnlyList<CredentialStatus> credentials)
    {
        Provider = provider;
        Model = model;
        Credentials = credentials;
    }

    /// <summary>Resolved provider id, lower-cased. Defaults to "openai".</summary>
    public string Provider { get; }

    /// <summary>Resolved model / deployment id, or empty string if unset.</summary>
    public string Model { get; }

    /// <summary>Presence (not value) of the credentials relevant to the chosen provider.</summary>
    public IReadOnlyList<CredentialStatus> Credentials { get; }

    /// <summary>Builds a config snapshot from the current process environment.</summary>
    public static HarnessConfig FromEnvironment()
    {
        string provider = (Environment.GetEnvironmentVariable(ProviderVar) ?? "openai").Trim().ToLowerInvariant();
        string model = (Environment.GetEnvironmentVariable(ModelVar) ?? string.Empty).Trim();

        return new HarnessConfig(provider, model, RelevantCredentials(provider));
    }

    private static IReadOnlyList<CredentialStatus> RelevantCredentials(string provider) => provider switch
    {
        "openai" => Status("OPENAI_API_KEY"),
        "local" => Status("LOCAL_BASE_URL", "LOCAL_API_KEY"),
        "azure" => Status("AZURE_OPENAI_ENDPOINT", "AZURE_OPENAI_API_KEY", "AZURE_OPENAI_DEPLOYMENT"),
        "anthropic" => Status("ANTHROPIC_API_KEY"),
        _ => Array.Empty<CredentialStatus>(),
    };

    private static CredentialStatus[] Status(params string[] names)
    {
        var result = new CredentialStatus[names.Length];
        for (int i = 0; i < names.Length; i++)
        {
            bool present = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(names[i]));
            result[i] = new CredentialStatus(names[i], present);
        }

        return result;
    }
}

/// <summary>Whether a single named credential env var is present. Value is never captured.</summary>
public readonly record struct CredentialStatus(string Name, bool Present);
