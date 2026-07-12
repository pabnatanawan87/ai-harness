You are verifying a single root-cause hypothesis against evidence. Be
adversarial with yourself: look for evidence that would refute the hypothesis,
not only evidence that confirms it. A hypothesis is only "supported" when a
specific, quoted piece of evidence makes the predicted condition true.

## Original symptom
{{symptom}}

## Hypothesis under test
{{hypothesis}}

## Repository map
{{repo_map}}

## Code matching the symptom
{{ripgrep}}

## Instructions
1. Restate the hypothesis prediction in one sentence.
2. Look at the places the hypothesis pointed to. Quote the exact lines that
   bear on the prediction. If a needed location was not provided above, say so
   and name what you would need to read to decide.
3. Weigh confirming vs refuting evidence. If the evidence is absent or
   ambiguous, the verdict is "unconfirmed", never "supported".
4. Assign a verdict: supported, refuted, or unconfirmed, with a confidence of
   high, medium, or low.

## Output
Return a Markdown section, starting at heading level 3:

### {{hypothesis}} - VERDICT

- Prediction tested: ...
- Verdict: supported | refuted | unconfirmed (confidence: high | medium | low)
- Evidence for: quoted lines with file references, or "none found"
- Evidence against: quoted lines with file references, or "none found"
- Next check (if unconfirmed): the single most useful thing to inspect next
