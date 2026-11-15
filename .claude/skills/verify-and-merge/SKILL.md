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
3. The PR merges cleanly with the current dev. After `git fetch origin`, run
   `git merge-tree --write-tree --name-only origin/dev <sha>`. If the output shows `CONFLICT` (a conflicted merge
   also exits with code 1), send the PR back to merge `origin/dev`.
4. When dev moved since the PR's base and the PR and dev changed the same files, build, test and format the merge
   result in a scratch worktree:
   - `git worktree add --detach <scratch> <sha>`
   - `git -C <scratch> merge origin/dev`
   - run the checks in item 5 from `<scratch>/src/NexGrades/`
   - `git worktree remove <scratch>` afterwards.
5. The CI-equivalent checks pass in the PR's worktree. Run them from `src/NexGrades/` in a subshell (Git Bash
   syntax), so the current directory does not change:

   ```sh
   (cd "<worktree>/src/NexGrades" && CI=true dotnet build NexGrades.slnx -warnaserror && dotnet test && dotnet format NexGrades.slnx --verify-no-changes)
   ```
6. Diff sanity check on `git diff --name-status origin/dev...<sha>` and the added lines:
   - No migration file is hand-edited: existing migrations are unchanged in the diff.
   - `.claude/settings.json` is not changed.
   - Every added `SuppressMessage` has a `Justification`.
   - The changed files match the scope of the issue.
7. The latest reviewer verdict at that exact SHA has no blocker or major finding and says "Ready to merge".
8. For a PR that touches `NexGrades.App`: the smoke start passed at that SHA. An agent runs it with
   `.claude/skills/smoke-start/SKILL.md`, never the owner.
9. The PR body has `Closes #<n>`, the decisions, the tests, the warning counts, and a manual UI test plan where the
   change needs one.
10. Follow-up issues are filed and linked.
11. Decisions left to the implementer are shown to the owner before the merge.
12. A new convention in `AGENTS.md` has the owner's OK before the merge.
13. No `NoWarn` was added.
14. Any worker action that was denied is reported to the owner, never redone.

## Merge

Squash-merge only when every item holds.

- Set `expectedHeadSha` to the head SHA.
- The commit title is the exact PR title followed by ` (#<pr>)`.

After the merge:

1. Remove that issue's worktree.
2. Update local `dev`.
3. Update `HANDOVER.md` (see `.claude/skills/lead-handover/SKILL.md`).

Merging `dev` into `main` is the owner's call only.
