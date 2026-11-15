---
name: verify-and-merge
description: The lead's checklist for an orchestrator's MERGE-READY report on a NexGrades pull request, followed by the squash-merge when every item holds. Use when an issue-orchestrator reports MERGE-READY. Lead session only.
argument-hint: <pr-number>
disable-model-invocation: true
---

# Verify and merge PR #$ARGUMENTS

Only the lead runs this. Orchestrators never merge. Start from the orchestrator's `MERGE-READY: PR #<n> @ <head sha>`
line and check every item yourself; don't rely on the report.

## Checklist

1. The PR head SHA equals the SHA in the MERGE-READY line.
2. All check runs on that SHA are completed and green, including SonarCloud.
3. The latest reviewer verdict at that exact SHA has no blocker or major finding and says "Ready to merge".
4. For a PR that touches `NexGrades.App`: the smoke start passed at that SHA. An agent runs it with
   `.claude/skills/smoke-start/SKILL.md`, never the owner.
5. The PR body has `Closes #<n>`, the decisions, the tests, the warning counts, and a manual UI test plan where the
   change needs one.
6. Follow-up issues are filed and linked.
7. Decisions left to the implementer are shown to the owner before the merge.
8. A new convention in `AGENTS.md` has the owner's OK before the merge.
9. No `NoWarn` was added.
10. Any worker action that was denied is reported to the owner, never redone.

## Merge

Squash-merge only when every item holds.

- Set `expectedHeadSha` to the head SHA.
- The commit title is the exact PR title followed by ` (#<pr>)`.

After the merge:

1. Remove that issue's worktree.
2. Update local `dev`.
3. Update `HANDOVER.md` (see `.claude/skills/lead-handover/SKILL.md`).

Merging `dev` into `main` is the owner's call only.
