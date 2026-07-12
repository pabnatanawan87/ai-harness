# Build Principles

Read this before starting any project in this repo. Every seed here is built under the same rules.

## 1. Provider-agnostic
Never hard-wire a single LLM vendor. All model calls go through one thin abstraction with a
pluggable backend (OpenAI, Anthropic, Azure OpenAI, or a local model) chosen by config/env at
runtime. Rationale: these tools must run at any future employer regardless of which AI they have,
and a portfolio reviewer may run them on whichever key they own. The abstraction is also the
personal insurance policy: the workflow survives a change of employer or vendor.

## 2. Clean-room - no employer IP
Zero employer code, stored procedures, schema, data, ticket IDs, colleague names, or internal
metrics. These tools re-implement GENERIC versions of methodologies from scratch. The ideas
(adversarial review, RCA decomposition, multi-agent orchestration, enablement metrics) are portable
intellectual approaches; any specific employer implementation is not. When in doubt, leave it out.

## 3. Secrets in env, never in code
API keys and tokens come from environment variables or a local `.env` that is gitignored. Nothing
secret is ever committed. Every project ships a `.env.example`.

## 4. Personal-time only
This is personal-time work, not employer work product.

## 5. Legible over clever
These double as portfolio pieces. A reviewer must be able to read the core loop in one sitting.
Prefer a small, well-documented orchestration you wrote over a heavy framework that hides it. A
framework (LangGraph, etc.) is optional, never required. The value on display is your judgment about
how agents should be composed and verified, so that judgment must be visible in the code.

## 6. Portfolio-ready by default
Each project ships: a README that explains the "why" and shows a real run (output sample or GIF),
an MIT `LICENSE`, a one-command quickstart, and a short "design notes" section. Assume a hiring
manager reads it in 5 minutes and may run it in 15.

## 7. Repos start private, go public deliberately
This `Internal` repo is private. When a project is polished and IP-clean, publish it to its own
public repo (or flip a copy public) so it can be linked from the resume and LinkedIn Featured
section. Never publish until a clean-room review has been done.

## 8. Style
Plain ASCII in all docs and output. No em dashes, en dashes, smart quotes, or arrow glyphs. Use
hyphens, "to", and "->".
