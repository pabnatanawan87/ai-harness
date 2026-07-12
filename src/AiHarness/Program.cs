using AiHarness;
using AiHarness.Cli;
using AiHarness.Composition;

// Load .env before anything reads configuration. Ambient environment always wins.
DotEnv.Load();

// Build the real modules once, then hand the wired command tree to System.CommandLine.
// Every subcommand (run, skills, config, new-skill) is defined in the Cli module; this
// entry point only composes the concrete modules and dispatches.
CliServices services = HarnessComposition.Build();

return CliApplication.Build(services).Parse(args).Invoke();
