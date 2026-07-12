using AiHarness.Cli.Abstractions;
using AiHarness.Providers;
using AiHarness.Rendering;

using Microsoft.Extensions.AI;

using Spectre.Console;

namespace AiHarness.Cli;

/// <summary>
/// The heart of "ai run": the small, readable orchestration that ties the pieces together
/// in one place. It is intentionally linear so a reader can follow a run end to end:
///
///   SkillCatalog.Get -> ContextGatherer -> PromptRenderer -> IChatClient -> PostProcessor
///
/// The provider is resolved lazily, only when a run actually needs the model, so listing
/// or showing skills never requires a configured key. The one vendor seam stays inside
/// <see cref="ProviderFactory"/>; this class only ever sees <see cref="IChatClient"/>.
/// </summary>
public sealed class RunPipeline
{
    private readonly CliServices _services;
    private readonly IAnsiConsole _console;
    private readonly Func<IChatClient> _clientFactory;

    /// <summary>
    /// Creates a pipeline. <paramref name="clientFactory"/> defaults to building a client
    /// from the environment via <see cref="ProviderFactory"/>; tests pass a fake instead.
    /// </summary>
    public RunPipeline(
        CliServices services,
        IAnsiConsole? console = null,
        Func<IChatClient>? clientFactory = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        _services = services;
        _console = console ?? AnsiConsole.Console;
        _clientFactory = clientFactory ?? DefaultClientFactory;
    }

    /// <summary>
    /// Runs one skill and returns a process exit code (0 on success, non-zero on a handled
    /// error). All expected failures - unknown skill, missing provider configuration - are
    /// reported as friendly messages rather than stack traces.
    /// </summary>
    public async Task<int> RunAsync(
        string skillName,
        RunOptions options,
        CancellationToken cancellationToken)
    {
        SkillInfo skill;
        try
        {
            skill = _services.SkillCatalog.Get(skillName);
        }
        catch (SkillNotFoundException ex)
        {
            _console.MarkupLineInterpolated($"[red]{ex.Message}[/]");
            return 1;
        }

        IChatClient client;
        try
        {
            client = _clientFactory();
        }
        catch (ProviderConfigurationException ex)
        {
            _console.MarkupLineInterpolated($"[red]Provider not ready:[/] {ex.Message}");
            return 1;
        }

        using (client)
        {
            try
            {
                GatheredContext context =
                    await _services.ContextGatherer.GatherAsync(skill, options, cancellationToken);

                IReadOnlyList<ChatMessage> messages =
                    _services.PromptRenderer.Render(skill, context);

                ChatResponse response =
                    await client.GetResponseAsync(messages, cancellationToken: cancellationToken);

                PostProcessResult result =
                    await _services.PostProcessor.ProcessAsync(skill, response, options, cancellationToken);

                if (result.WrittenPath is not null)
                {
                    _console.MarkupLineInterpolated($"[green]Wrote[/] {result.WrittenPath}");
                }
                else
                {
                    // Print the raw text without markup interpretation so model output that
                    // contains bracket characters is never mangled or misread as Spectre markup.
                    _console.WriteLine(result.Text);
                }
            }
            catch (TemplateRenderException ex)
            {
                _console.MarkupLineInterpolated($"[red]Prompt template error:[/] {ex.Message}");
                return 1;
            }
            catch (StructuredOutputException ex)
            {
                _console.MarkupLineInterpolated($"[red]Model did not return valid JSON:[/] {ex.Message}");
                return 1;
            }
            catch (IOException ex)
            {
                _console.MarkupLineInterpolated($"[red]I/O error:[/] {ex.Message}");
                return 1;
            }
            catch (OperationCanceledException)
            {
                _console.MarkupLine("[yellow]Cancelled.[/]");
                return 130;
            }
            catch (Exception ex)
            {
                // The model call (or a backend HTTP failure) can throw provider-specific
                // exception types. This is the CLI boundary, so report the message rather
                // than dumping a stack trace, and exit non-zero.
                _console.MarkupLineInterpolated($"[red]Run failed:[/] {ex.Message}");
                return 1;
            }
        }

        return 0;
    }

    private static IChatClient DefaultClientFactory()
    {
        HarnessConfig config = HarnessConfig.FromEnvironment();
        return ProviderFactory.Create(config);
    }
}
