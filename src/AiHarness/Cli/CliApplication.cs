using System.CommandLine;

using AiHarness.Cli.Commands;

using Spectre.Console;

namespace AiHarness.Cli;

/// <summary>
/// Builds the fully wired ai-harness command tree. This is the CLI module's single public
/// entry point: the program's Main just loads the environment, constructs the
/// <see cref="CliServices"/> from the concrete modules, and calls
/// <c>CliApplication.Build(services).Parse(args).Invoke()</c>.
///
/// Keeping all wiring here (rather than scattered across Main) means the command surface
/// can be built with fake services in a test and exercised without touching a real model,
/// filesystem, or provider key.
/// </summary>
public static class CliApplication
{
    /// <summary>
    /// Builds the root command with every subcommand wired: run, skills (list/show),
    /// config, and new-skill.
    /// </summary>
    /// <param name="services">The collaborators the run/skills commands depend on.</param>
    /// <param name="console">Console sink; defaults to the shared <see cref="AnsiConsole"/>.</param>
    public static RootCommand Build(CliServices services, IAnsiConsole? console = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        IAnsiConsole sink = console ?? AnsiConsole.Console;

        var root = new RootCommand(
            "ai-harness - a provider-agnostic CLI that runs reusable skills against any LLM backend.");

        root.Subcommands.Add(RunCommand.Create(services, sink));
        root.Subcommands.Add(SkillsCommand.Create(services.SkillCatalog, sink));
        root.Subcommands.Add(ConfigCommand.Create(sink));
        root.Subcommands.Add(NewSkillCommand.Create(sink));

        return root;
    }
}
