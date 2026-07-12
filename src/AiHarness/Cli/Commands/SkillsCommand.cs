using System.CommandLine;

using AiHarness.Cli.Abstractions;

using Spectre.Console;

namespace AiHarness.Cli.Commands;

/// <summary>
/// "ai skills list" and "ai skills show &lt;name&gt;" - browse the skills the catalog can
/// see. "list" prints a table of name and description; "show" prints a skill's metadata
/// and its raw skill.yaml so a user can inspect exactly what will run.
/// </summary>
public static class SkillsCommand
{
    public static Command Create(ISkillCatalog catalog, IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(console);

        var command = new Command("skills", "List and inspect available skills.");
        command.Subcommands.Add(CreateList(catalog, console));
        command.Subcommands.Add(CreateShow(catalog, console));
        return command;
    }

    private static Command CreateList(ISkillCatalog catalog, IAnsiConsole console)
    {
        var command = new Command("list", "List all available skills.");

        command.SetAction(_ =>
        {
            IReadOnlyList<SkillInfo> skills = catalog.List();
            if (skills.Count == 0)
            {
                console.MarkupLine("[yellow]No skills found.[/] Create one with 'ai new-skill <name>'.");
                return 0;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Skill");
            table.AddColumn("Description");

            foreach (SkillInfo skill in skills)
            {
                table.AddRow(Markup.Escape(skill.Name), Markup.Escape(skill.Description));
            }

            console.Write(table);
            return 0;
        });

        return command;
    }

    private static Command CreateShow(ISkillCatalog catalog, IAnsiConsole console)
    {
        var nameArg = new Argument<string>("name")
        {
            Description = "Name of the skill to show.",
        };

        var command = new Command("show", "Show a skill's metadata and its raw manifest.");
        command.Arguments.Add(nameArg);

        command.SetAction(parseResult =>
        {
            string name = parseResult.GetValue(nameArg) ?? string.Empty;

            SkillInfo skill;
            try
            {
                skill = catalog.Get(name);
            }
            catch (SkillNotFoundException ex)
            {
                console.MarkupLineInterpolated($"[red]{ex.Message}[/]");
                return 1;
            }

            var table = new Table().Border(TableBorder.Rounded);
            table.AddColumn("Field");
            table.AddColumn("Value");
            table.AddRow("name", Markup.Escape(skill.Name));
            table.AddRow("description", Markup.Escape(skill.Description));
            table.AddRow(
                "inputs",
                skill.Inputs.Count == 0 ? "[grey](none)[/]" : Markup.Escape(string.Join(", ", skill.Inputs)));
            table.AddRow(
                "output",
                string.IsNullOrEmpty(skill.OutputFormat) ? "[grey](unset)[/]" : Markup.Escape(skill.OutputFormat));
            table.AddRow("path", Markup.Escape(skill.SourcePath));
            console.Write(table);

            string manifestPath = Path.Combine(skill.SourcePath, "skill.yaml");
            if (File.Exists(manifestPath))
            {
                string manifest = File.ReadAllText(manifestPath);
                console.Write(new Panel(Markup.Escape(manifest))
                {
                    Header = new PanelHeader("skill.yaml"),
                    Border = BoxBorder.Rounded,
                });
            }

            return 0;
        });

        return command;
    }
}
