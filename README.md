# ai-harness

A small, provider-agnostic CLI that runs reusable "skills" (RCA, PR review, commit messages,
refactors, and more) against any LLM backend. Point it at OpenAI, a local OpenAI-compatible
server, or (planned) Azure OpenAI and Anthropic by flipping one environment variable. Your
workflow, made vendor-independent and version-controlled.

It is a batch/CLI runner, not an IDE, a chat UI, or a heavy agent framework. The whole run
loop is meant to be read in one sitting.

## Why

Good engineering habits (decompose a symptom into verified hypotheses, review a diff
adversarially, write a clean commit message) are portable. The AI vendor you happen to have
is not. ai-harness keeps the habits as data-defined skills and routes every model call
through a single seam, so the same skills keep working when you change model, key, or
employer.

## Quickstart

Requires the .NET 10 SDK.

```
# 1. Configure a provider (secrets live only in .env, which is gitignored).
cp .env.example .env
#    edit .env: set AIHARNESS_PROVIDER, AIHARNESS_MODEL, and the matching key.

# 2. See what is available and run a skill.
dotnet run --project src/AiHarness -- skills list
dotnet run --project src/AiHarness -- run explain --file src/AiHarness/Program.cs
```

Install it as a global tool named `ai`:

```
dotnet pack src/AiHarness -c Release
dotnet tool install --global --add-source src/AiHarness/bin/Release AiHarness
ai skills list
```

## Configuration

Configuration comes from environment variables (or a local `.env`; the ambient environment
always wins). No secret is ever read into code beyond the one provider factory.

```
AIHARNESS_PROVIDER=openai        # openai | local | azure (planned) | anthropic (planned)
AIHARNESS_MODEL=gpt-4o-mini      # model / deployment id for the chosen provider
OPENAI_API_KEY=...               # for provider=openai
LOCAL_BASE_URL=http://localhost:11434/v1   # for provider=local (Ollama, vLLM, etc.)
```

`ai config` prints the resolved provider and model and which credentials are present. It
never prints secret values:

```
$ ai config
+----------------+---------+
| Setting        | Value   |
+----------------+---------+
| provider       | openai  |
| model          | gpt-4o-mini |
| OPENAI_API_KEY | present |
+----------------+---------+
```

## Commands

- `ai run <skill> [--file f] [--diff] [--input "..."] [--out path]` - gather context, render
  the prompt, call the model, print or write the result.
- `ai skills list` - list available skills.
- `ai skills show <name>` - show a skill's metadata and its raw manifest.
- `ai config` - print the resolved provider/model and credential presence.
- `ai new-skill <name>` - scaffold a new skill folder under ./skills.

## Skills

Skills are data, not code. A skill is a folder with a `skill.yaml` manifest plus one or more
prompt templates. Adding or editing a skill needs no recompile.

```
$ ai skills list
+---------------+------------------------------------------------------------------+
| Skill         | Description                                                      |
+---------------+------------------------------------------------------------------+
| commit-msg    | Draft a Conventional Commits message from the staged diff        |
| explain       | Explain a file or function for onboarding                        |
| pr-review     | Review the diff for correctness, security, and test gaps, with   |
|               | an optional refute pass                                          |
| rca           | Decompose a symptom into verified root-cause hypotheses          |
| refactor      | Propose a safe refactor with rationale and no behavior change    |
| spec-to-tests | Propose test cases and test skeletons for a file or written spec |
+---------------+------------------------------------------------------------------+
```

Real runs:

```
# Explain an unfamiliar file for onboarding.
ai run explain --file src/AiHarness/Cli/RunPipeline.cs

# Draft a Conventional Commit message from your staged changes.
git add -A
ai run commit-msg

# Review your working-tree diff for correctness/security/test gaps.
ai run pr-review --diff

# Decompose a bug report into verified root-cause hypotheses, write the report to a file.
ai run rca --input "users intermittently see a 500 on checkout" --out rca.md

# Propose test cases and skeletons for a file (pipe extra spec notes on stdin).
ai run spec-to-tests --file src/AiHarness/Rendering/PromptRenderer.cs
```

### Authoring a skill

`ai new-skill mytask` writes `skills/mytask/skill.yaml` and `skills/mytask/prompts/main.md`.
The manifest shape (documented in full at the top of `skills/rca/skill.yaml`):

```yaml
name: rca
description: Decompose a symptom into verified root-cause hypotheses
inputs: [symptom]
context:
  - repo_map                 # built-in gatherer -> {{repo_map}}
  - ripgrep: "{{symptom}}"   # built-in gatherer -> {{ripgrep}}
steps:
  - id: decompose
    prompt: prompts/decompose.md
    produces: hypotheses
output: markdown
```

Prompt templates use double-brace placeholders, for example `{{symptom}}` and `{{files}}`.
Double braces are deliberate so a literal single-brace JSON example inside a prompt body is
never mistaken for a placeholder. The built-in context gatherers are `files`, `diff`,
`ripgrep`, `repo_map`, and `stdin`; each exposes its result under the matching placeholder.

Skills are discovered across an ordered search path, first match wins, so a project-local
skill overrides a personal one, which overrides the built-ins:

1. `<cwd>/skills`
2. `<user home>/.ai-harness/skills`
3. `<app dir>/skills` (bundled with the installed tool)

## Design notes

- One vendor seam. Every model call goes through `Microsoft.Extensions.AI`'s `IChatClient`.
  The only file that references a vendor SDK is `src/AiHarness/Providers/ProviderFactory.cs`.
  To add or swap a backend, change that one file.
- Legible pipeline. `ai run` is a short, linear flow you can read top to bottom:
  `SkillCatalog.Get -> ContextGatherer -> PromptRenderer -> IChatClient -> PostProcessor`
  (see `src/AiHarness/Cli/RunPipeline.cs`).
- Composition root. The concrete modules (Skills, Context, Rendering, PostProcessor) are
  wired to the CLI's small interfaces by thin adapters in `src/AiHarness/Composition/`, so
  each module is built and tested in isolation.
- Structured output. When a skill wants JSON, the post-processor asks for JSON and applies a
  single corrective retry if the first reply is not valid JSON, tolerating code fences and
  surrounding prose.
- The current runner is single-shot (one prompt, one response). Multi-step and `foreach`
  orchestration is expressed in the manifests and is the next milestone; the seams are shaped
  so a future runner drops in without changing the CLI.

## Testing

```
dotnet test
```

Orchestration tests use a fake `IChatClient` that returns canned responses, so the default
suite spends no tokens. There is also one live integration test that actually calls the
configured provider; it is gated on an environment variable:

```
# set your provider env vars first, then:
AIHARNESS_LIVE=1 dotnet test --filter Category=Live
```

## Related

- agentic-code-review - a sibling project focused on adversarial, multi-pass code review.
  ai-harness nods to it with the lightweight `pr-review` skill and its optional refute pass.

## License

MIT. See [LICENSE](LICENSE).
