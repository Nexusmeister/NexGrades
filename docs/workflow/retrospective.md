# Workflow retrospective

A running log of what the agent workflow (lead, `issue-orchestrator` and the role agents) learned from each
merged pull request. Its purpose is to find repeated friction and fix it in the agents, skills or docs, instead
of paying for the same mistake again.

## Where entries come from

- The `issue-orchestrator` has `docs-writer` (haiku) write the entry inside the issue's own pull request, from its
  "Workflow notes", once the review says "Ready to merge".
- The lead adds its own findings from `verify-and-merge` before the merge, the same way, in the same PR.
- This is one file. When two open PRs both add an entry, the one that merges second merges `origin/dev` and
  `docs-writer` (haiku) resolves the conflict, keeping both entries, newest first.
- After the merge, the lead checks the log for repeats with the `retrospective` skill
  (`.claude/skills/retrospective/SKILL.md`).

## Rules

- **No pupil data.** No names, grades, note text or record ids, in an entry or anywhere else in this file.
- **Entry format.** One entry per PR, newest first, under "Entries":

  ```markdown
  ### YYYY-MM-DD: PR #<pr> (issue #<n>) <short title>

  - **Cost and friction:** what went wrong or cost extra: blocked agents, retries, review findings a role should
    have caught, repeated fixes. Write "none" if nothing did.
  - **Lessons:** what to do differently, in one line each.
  - **Repeat check:** earlier entries with the same lesson (PR numbers), or "none".
  ```

- **Escalation.** A lesson that appears in two entries is a repeat. The lead then proposes an issue that changes
  an agent, a skill or a docs file, and opens it with the owner's OK. Nothing in `.claude/` or `AGENTS.md`
  changes without the owner's OK.

## Entries

### 2026-11-24: PR #198 (issue #173) Workflow retrospective log and skill

- **Cost and friction:** the role agents ran in the background despite the brief. One owner round was needed
  because the skill left the commit route open.
- **Lessons:** a skill that tells the lead to commit must name the route.
- **Repeat check:** none.

### 2026-11-24: PR #195 (issue #170) Dependabot configuration

- **Cost and friction:** GitHub's validator rejected `target-branch`, `commit-message` and
  `open-pull-requests-limit` on multi-ecosystem member entries (one fix round). The first review found a major:
  duplicate nuget entries. The orchestrator reported MERGE-READY while two non-required "Automatic Dependency
  Submission (NuGet)" jobs failed (Linux restore of the Windows-only projects). An owner decision followed, then a
  second orchestrator round with `EnableWindowsTargeting`.
- **Lessons:** read the failing check's annotations (public API) before reporting. MERGE-READY needs every check
  green, so a failing non-required check is a question for the lead, not a caveat.
- **Repeat check:** none.

### 2026-11-24: PR #196 (issue #169) Handoff skill

- **Cost and friction:** the orchestrator reported `BLOCKED: CI timeout` after 30 rounds on a Markdown-only PR.
  CI was only slow and finished green; the lead re-checked and merged. The reviewer (haiku) found 2 minors:
  `HANDOVER.md` was not in `.gitignore` (filed as #197).
- **Lessons:** slow CI is not a failure; the lead re-checks the check runs before acting on a timeout.
- **Repeat check:** none.
