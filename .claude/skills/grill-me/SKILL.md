---
name: grill-me
description: Interviews the user relentlessly about a plan, feature or design, one question at a time, until every open decision is settled and both sides share the same understanding. Use when the user says "grill me", when requirements are vague, or before planning a non-trivial change.
---

# Grill me

Your goal is shared understanding before any work starts. Interview the user about the plan or idea until
no open decisions remain.

## How to run the interview

- **Ask one question at a time** and wait for the answer. Never send a list of questions.
- **Walk the decision tree.** Start with the decisions that others depend on (scope, who uses it, what
  "done" means), then go down each branch. Every answer can open new questions; follow them.
- **Recommend an answer for every question**, with a one-line reason, so the user can just confirm.
- **Look things up instead of asking.** If the code, `AGENTS.md`, the issue or git history can answer a
  question, read them and state what you found. Only ask the user what only the user can decide.
- **Push back** when an answer conflicts with something earlier, with the domain invariants in `AGENTS.md`,
  or with how the code works today. Name the conflict plainly and ask which way to go.
- **Probe the edges**, since grade books fail there:
  - a student who changes class mid-year
  - an exempt student
  - a closed school year
  - an empty bucket
  - a weight sum that isn't 100
  - rollover into the next year
  - what the German UI will need later
- Don't be satisfied with "it depends" or "probably". Get a decision, or record it explicitly as deferred.

## When to stop

Stop when every branch has a decision or an explicit deferral, or when the user says to stop.

## Finish with a summary

Close with a short, numbered list of every decision made: each one a single line with the chosen option.
Then list the deferred questions. Offer to post the summary as a comment on the related GitHub issue, and
post it only if the user agrees.
