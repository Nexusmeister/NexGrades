---
name: retrospective
description: The lead's step after a merge. Records what the workflow cost and learned in docs/workflow/retrospective.md, checks earlier entries for a repeated lesson, proposes an issue when one repeats and feeds the lessons into HANDOVER.md. Use after every squash-merge, before updating HANDOVER.md. Lead session only.
argument-hint: <pr-number>
---

# Retrospective for PR #$ARGUMENTS

Only the lead runs this, once per merged PR. Orchestrators and role agents don't.

## Steps

1. **Gather.** Collect the "Workflow notes" section of the orchestrator's final report and your own findings from
   `verify-and-merge` (items that failed first time, denied worker actions, anything you sent back).
2. **Write the entry.** Read `docs/workflow/retrospective.md` first, then add one entry at the top of "Entries"
   in the format given there: date, PR and issue, cost and friction, lessons, repeat check. Write "none" where
   nothing applies; never invent friction. No pupil data: no names, grades, note text or record ids.
3. **Check for a repeat.** Compare the lessons with the earlier entries. A lesson that appears in two entries is a
   repeat. Name the earlier PRs in the "Repeat check" line.
4. **Escalate a repeat.** Propose an issue that changes the agent, skill or docs file behind the lesson, and show
   it to the owner. Nothing in `.claude/` or `AGENTS.md` changes without the owner's OK; open the issue only
   with it.
5. **Feed the handoff.** Put the lessons into `HANDOVER.md` (see `.claude/skills/handoff/SKILL.md`): reusable
   approaches under "What worked", failed approaches and pitfalls under "What didn't work". `HANDOVER.md` is local
   and never committed; rewrite the whole file as the handoff skill says.

The entry in `docs/workflow/retrospective.md` is a docs change. Commit it the way the lead commits other docs
changes; don't commit `HANDOVER.md`.
