# KICKSTART - ai-harness

Paste into Claude Code (or ChatGPT) started inside this folder.

---

> You are helping me build `ai-harness`, a provider-agnostic personal agentic CLI in .NET. First read
> `../BUILD_PRINCIPLES.md`, then `README.md` and `DESIGN.md` here. Treat DESIGN.md as the spec.
>
> Hard rules: provider-agnostic (all model calls go through `Microsoft.Extensions.AI`'s `IChatClient`;
> only the provider registration references a vendor package); clean-room (skills are generic
> engineering tasks, no employer-specific content); secrets from env only with a `.env.example`;
> plain ASCII, no em dashes; small and legible.
>
> Work milestone by milestone (DESIGN.md section 7), stopping for my review after each. Start with
> M1: the provider abstraction over `IChatClient` with an OpenAI backend and `ai config`. Show me the
> provider registration and prove a hello prompt runs from config, then stop.
>
> Design skills as DATA (skill.yaml + prompt templates) per DESIGN.md section 3.2 so I can add skills
> without recompiling. Confirm the stack in section 5 (.NET 8+, Microsoft.Extensions.AI,
> System.CommandLine, YamlDotNet, Spectre.Console, xUnit) or propose better with reasons.

---

## Notes for future-you
- This is your continuity tool. On a new job, set `AIHARNESS_PROVIDER` to whatever they have and your
  skills keep working. If they are Python-only, the same design ports; keep the abstraction.
- Leverages the OpenAI + Microsoft.Extensions.AI experience you already have. Build this one when you
  want your daily workflow back fast; build the Python flagship when you want the public showcase.
