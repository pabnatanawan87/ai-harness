using System.CommandLine;

using Spectre.Console;

namespace AiHarness.Cli.Commands;

/// <summary>
/// "ai config" - print the resolved provider and model and which credential env vars are
/// present. Keys-present only: secret values are never read or shown (see
/// <see cref="HarnessConfig"/>, which is the display-safe surface).
/// </summary>
public static class ConfigCommand
{
    public static Command Create(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);

        var command = new Command(
            "config",
            "Print the resolved provider and model, and which credential env vars are present.");

        command.SetAction(_ =>
        {
            HarnessConfig config = HarnessConfig.FromEnvironment();

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Setting");
            table.AddColumn("Value");

            table.AddRow("provider", Markup.Escape(config.Provider));
            table.AddRow(
                "model",
                string.IsNullOrEmpty(config.Model) ? "[grey](unset)[/]" : Markup.Escape(config.Model));

            foreach (CredentialStatus credential in config.Credentials)
            {
                string state = credential.Present ? "[green]present[/]" : "[red]missing[/]";
                table.AddRow(Markup.Escape(credential.Name), state);
            }

            console.Write(table);
            return 0;
        });

        return command;
    }
}
