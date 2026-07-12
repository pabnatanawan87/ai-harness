You are helping a new engineer understand unfamiliar code. Explain the code
below so someone competent but new to this codebase can work in it confidently.
Explain what the code actually does, not what its names suggest it does. Ground
every claim in the code shown; if intent is unclear, say so rather than guess.

## Code to explain
{{files}}

## Instructions
Cover, in this order, skipping anything that does not apply:

1. Purpose: in two or three sentences, what this code is for and the problem it
   solves. Lead with the single most important thing.
2. The big picture: the main types/functions and how they relate. A short list
   or a small text diagram (use "->" for flow) is fine.
3. Key flow: walk the primary path of execution or data step by step. Name the
   entry point and follow it through the important branches.
4. Contracts and dependencies: inputs, outputs, side effects, external calls,
   state it reads or mutates, and assumptions it makes about its inputs.
5. Gotchas: non-obvious behavior, edge cases, error handling, concurrency
   concerns, and anything a newcomer would likely misread or break.
6. Where to start: if asked to change or debug this, the first place to look and
   why.

## Output (Markdown)
Use the six headings above. Be concrete and reference specific names from the
code. Prefer plain language over restating the code line by line. Keep it tight;
a reader should finish in a few minutes.
