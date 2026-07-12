using System.CommandLine;

using Spectre.Console;

namespace AiHarness.Cli.Commands;

/// <summary>
/// "ai new-skill &lt;name&gt;" - scaffold a new skill folder (skill.yaml + a prompt
/// template) under ./skills so a user can start authoring immediately. Skills are data,
/// so a new one needs no recompile. The actual file writing lives in
/// <see cref="SkillScaffolder"/>.
/// </summary>
public static class NewSkillCommand
{
    public static Command Create(IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(console);

        var nameArg = new Argument<string>("name")
        {
            Description = "Name of the new skill, e.g. explain. Letters, digits, hyphens, underscores.",
        };

        var command = new Command("new-skill", "Scaffold a new skill folder under ./skills.");
        command.Arguments.Add(nameArg);

        command.SetAction(parseResult =>
        {
            string name = parseResult.GetValue(nameArg) ?? string.Empty;
            SkillScaffolder.ScaffoldResult result = SkillScaffolder.Scaffold(name);

            if (result.Created)
            {
                console.MarkupLineInterpolated($"[green]{result.Message}[/]");
                console.MarkupLine("Edit [yellow]skill.yaml[/] and [yellow]prompts/main.md[/], then run it with 'ai run " + Markup.Escape(name) + "'.");
                return 0;
            }

            console.MarkupLineInterpolated($"[red]{result.Message}[/]");
            return 1;
        });

        return command;
    }
}
