---
name: retrospective
description: The lead's workflow retrospective for a pull request. Before the merge, checks that the PR carries its entry in docs/workflow/retrospective.md and adds the lead's own findings. After the merge, checks the log for a repeated lesson, proposes an issue when one repeats and feeds the lessons into HANDOVER.md. Use around every squash-merge, as part of verify-and-merge. Lead session only.
argument-hint: <pr-number>
---

# Retrospective for PR #$ARGUMENTS

Only the lead runs this, once per PR. Orchestrators and role agents don't run the skill; the orchestrator does write
the first version of the entry (see below).

The entry lives inside the PR, in `docs/workflow/retrospective.md`, so it merges with the PR. It is not committed
separately afterwards.

## Before the merge (inside the PR)

1. **The orchestrator's entry.** The orchestrator has `docs-writer` (haiku) add the entry at the top of "Entries",
   built from its "Workflow notes", before it reports MERGE-READY. Read the entry in the PR.
2. **Your findings.** Compare it with your own findings from `verify-and-merge`: items that failed first time,
   denied worker actions, anything you sent back. If they are missing and matter, delegate the edit to
   `docs-writer` (model: `haiku`) in the PR's worktree, commit and push it to the PR branch, and wait for green CI.
   An entry-only commit needs no new reviewer pass.
3. **No findings.** If you have none, the orchestrator's entry stands unchanged.

Write "none" where nothing applies; never invent friction. No pupil data: no names, grades, note text or record
ids.

## After the merge

1. **Check for a repeat.** Read the merged log in `docs/workflow/retrospective.md` on updated `dev`. A lesson that
   appears in two entries is a repeat. This check is the authoritative one: the "Repeat check" line the
   orchestrator or `docs-writer` filled in before the merge reflects the log as it was then. If that line is wrong
   or incomplete, correct it in the next docs change; never open a PR for it alone.
2. **Escalate a repeat.** Propose an issue that changes the agent, skill or docs file behind the lesson, and show
   it to the owner. Nothing in `.claude/` or `AGENTS.md` changes without the owner's OK; open the issue only
   with it.
3. **Feed the handoff.** Put the lessons into `HANDOVER.md` (see `.claude/skills/handoff/SKILL.md`): reusable
   approaches under "What worked", failed approaches and pitfalls under "What didn't work". `HANDOVER.md` is local
   and never committed; rewrite the whole file as the handoff skill says.
