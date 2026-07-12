using System.CommandLine;

using AiHarness.Cli.Abstractions;

using Spectre.Console;

namespace AiHarness.Cli.Commands;

/// <summary>
/// "ai run &lt;skill&gt; [--file f] [--diff] [--input "..."] [--out path]" - the main verb.
/// It gathers the flags into a <see cref="RunOptions"/> and hands the work to
/// <see cref="RunPipeline"/>, which performs the SkillCatalog -> ContextGatherer ->
/// PromptRenderer -> IChatClient -> PostProcessor flow.
/// </summary>
public static class RunCommand
{
    public static Command Create(CliServices services, IAnsiConsole console)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(console);

        var skillArg = new Argument<string>("skill")
        {
            Description = "Name of the skill to run (see 'ai skills list').",
        };

        var fileOption = new Option<string?>("--file", "-f")
        {
            Description = "Path to a file whose contents are added as context.",
        };

        var diffOption = new Option<bool>("--diff")
        {
            Description = "Include the working-tree/staged git diff as context.",
        };

        var inputOption = new Option<string?>("--input", "-i")
        {
            Description = "Free-text input for the skill's primary input.",
        };

        var outOption = new Option<string?>("--out", "-o")
        {
            Description = "Write the result to this path instead of printing it.",
        };

        var command = new Command(
            "run",
            "Run a skill against the configured model and print or write the result.");
        command.Arguments.Add(skillArg);
        command.Options.Add(fileOption);
        command.Options.Add(diffOption);
        command.Options.Add(inputOption);
        command.Options.Add(outOption);

        var pipeline = new RunPipeline(services, console);

        command.SetAction((parseResult, cancellationToken) =>
        {
            string skill = parseResult.GetValue(skillArg) ?? string.Empty;
            var options = new RunOptions(
                FilePath: parseResult.GetValue(fileOption),
                IncludeDiff: parseResult.GetValue(diffOption),
                Input: parseResult.GetValue(inputOption),
                OutPath: parseResult.GetValue(outOption));

            return pipeline.RunAsync(skill, options, cancellationToken);
        });

        return command;
    }
}
