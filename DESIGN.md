# ai-harness - Design

Read `../BUILD_PRINCIPLES.md` first. Clean-room: the skills are generic engineering tasks; ship no
employer-specific prompt content.

## 1. Goals and non-goals
Goals: a small, portable CLI that runs reusable skills against any LLM vendor, local-first, easy to
extend. Your working style, made vendor-independent and version-controlled.
Non-goals: not an IDE, not a full agent framework, not a chat UI. It is a batch/CLI runner.

## 2. Concepts
- Provider: a vendor-neutral chat/completion client. One interface, many backends.
- Skill: a named, reusable unit = manifest (`skill.yaml`) + prompt template(s) + optional pre-step
  (gather context: read files, run git, ripgrep) and post-step (write files, format output).
- Run: `ai run <skill> [inputs]` resolves the provider, gathers context, renders the prompt, calls
  the model (single-shot or a small multi-step loop), post-processes, and emits the result.

## 3. Architecture
```
CLI (System.CommandLine)
  -> SkillLoader (reads skill.yaml + templates from ./skills and a user skills dir)
  -> ContextGatherer (files, git diff, ripgrep, stdin)
  -> PromptRenderer (template + inputs + context)
  -> IChatClient (Microsoft.Extensions.AI)  <-- the one vendor seam
        backends: OpenAI, Azure OpenAI, Anthropic (community adapter or thin custom), Local (OpenAI-compatible)
  -> PostProcessor (parse structured output, write files, print)
```

### 3.1 Provider abstraction
Use `Microsoft.Extensions.AI`'s `IChatClient` as the interface. Register the backend by config:
```
AIHARNESS_PROVIDER=openai        # openai | azure | anthropic | local
AIHARNESS_MODEL=gpt-...
OPENAI_API_KEY= / AZURE_OPENAI_* / ANTHROPIC_API_KEY=
```
Nothing outside the provider registration references a vendor package. Structured output goes through
`IChatClient` tool/JSON support with a one-retry-on-invalid-JSON policy.

### 3.2 Skill format
```
skills/rca/skill.yaml
  name: rca
  description: Decompose a symptom into verified root-cause hypotheses
  inputs: [symptom]
  context:
    - repo_map            # optional built-in gatherers
    - ripgrep: "{symptom_keywords}"
  steps:
    - prompt: prompts/decompose.md      # -> hypotheses[]
    - foreach: hypotheses
      prompt: prompts/verify.md         # -> supported/unsupported + evidence
  output: markdown
```
Skills are data, not code, wherever possible, so adding one does not require recompiling. Built-in
gatherers (files, diff, ripgrep, repo_map) are the only code a skill leans on.

### 3.3 Built-in skill set (v1)
- `rca` - hypothesis decomposition + per-hypothesis source verification (the generic RCA method).
- `pr-review` - review the working/staged diff across correctness/security/tests; optional refute
  pass (nod to the sibling agentic-code-review project; keep it lightweight here).
- `spec-to-tests` - given a file or spec, propose test cases + skeletons.
- `commit-msg` - draft a conventional commit message from the staged diff.
- `refactor` - propose a safe refactor with rationale, no behavior change.
- `explain` - explain a file/function for onboarding.

## 4. CLI
- `ai run <skill> [--file f] [--diff] [--input "..."] [--out path]`
- `ai skills list` / `ai skills show <name>`
- `ai config` (print resolved provider/model, keys-present only)
- `ai new-skill <name>` (scaffold a skill folder)

## 5. Tech stack
.NET 8+, `Microsoft.Extensions.AI` (+ `Microsoft.Extensions.AI.OpenAI`), `System.CommandLine`,
`YamlDotNet` for skill manifests, `Spectre.Console` for output. xUnit for tests. Packaged as a
`dotnet tool`.

## 6. Repo layout
```
ai-harness/
  src/AiHarness/ Program.cs  Cli/  Providers/  Skills/  Context/  Rendering/
  skills/ rca/ pr-review/ spec-to-tests/ ...     # data-defined skills
  tests/
  .env.example  AiHarness.sln  README.md  DESIGN.md  LICENSE
```

## 7. Milestones
- M1: provider abstraction via IChatClient + OpenAI backend + `ai config`. Acceptance: a hello prompt
  runs on OpenAI from config.
- M2: skill loader + `ai run explain --file x` end to end. Acceptance: a data-defined skill runs.
- M3: context gatherers (diff, ripgrep, repo_map) + `rca` and `pr-review`. Acceptance: rca produces
  verified hypotheses on a sample repo.
- M4: Anthropic + local backends; prove a skill runs unchanged across providers by flipping env.
- M5: `commit-msg`, `spec-to-tests`, `refactor`, `new-skill` scaffolder, package as dotnet tool,
  README with real runs.

## 8. Testing
Fake `IChatClient` returning canned responses for orchestration tests (no token spend). Skill-loader
and renderer unit tests. One `--live` integration test per provider.

## 9. Portfolio note
Public-shippable as the ".NET agentic tooling" counterpart to the Python flagship. Together they show
range: Python for the AI-native audience, .NET for enterprise depth. Publish after a clean-room pass.
