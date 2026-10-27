---
name: implement-issue
description: Works a NexGrades GitHub issue end to end by orchestrating the project subagents through plan, implementation, tests, review, docs and a pull request against dev. Use when the user asks to implement, work on or pick up an issue by number.
argument-hint: <issue-number>
disable-model-invocation: true
---

# Implement issue #$ARGUMENTS

You are the orchestrator. Run this workflow from the main session: subagents cannot start other
subagents, so you delegate each step and carry the results between them. Read `AGENTS.md` first if it is
not already in context.

Keep the user informed with one short line per step. Stop and ask whenever a step says so.

## 1. Understand the issue

- Run `gh issue view $ARGUMENTS --comments`.
- If the goal, scope or acceptance criteria are unclear, run the `grill-me` skill with the user before
  planning. Don't start planning on guesses.

## 2. Branch

- Make sure the working tree is clean, then update `dev` and branch off it:
  `git switch dev`, `git pull`, `git switch -c feature/$ARGUMENTS-<short-slug>`.

## 3. Plan: `architect`

- Delegate to the `architect` subagent with the issue text and the decisions from step 1.
- Show the user the plan, especially its open questions and any invariants it touches.
- **Stop and wait for the user to approve or adjust the plan.**

## 4. Implement: `csharp-coder` and `ui-designer`

- Give `csharp-coder` the approved plan's Domain, Data and view-model parts.
- Give `ui-designer` the XAML parts once the view-model surface it binds to exists.
- Run them one after the other when they touch the same files; in parallel only when they don't.
- If either reports that the plan doesn't work, go back to step 3 with that finding.

## 5. Test: `tester`

- Give `tester` the plan's test list and the list of changed files.
- If it reports a production bug, send that to `csharp-coder`, then let `tester` re-run.

## 6. Verify yourself

From `src/NexGrades/`, run `dotnet build NexGrades.App/NexGrades.App.csproj` and `dotnet test`. Don't rely
only on the subagents' reports. Fix or delegate until both pass.

## 7. Review: `reviewer`

- Delegate to `reviewer` for the branch diff against `dev`.
- Send blockers and majors to the agent that owns the code, then review again.
- After two review rounds with blockers remaining, stop and show the user the open findings.

## 8. Docs: `docs-writer`

Only when the change affects behavior users see, commands, conventions or the domain model: let
`docs-writer` update `README.md`, `AGENTS.md` and the XML docs.

## 9. Pull request

- Commit with a short imperative subject, then push.
- Open a PR against `dev` with `gh pr create --base dev`. The body has a summary, the design decisions,
  the test coverage, anything the user must check by hand in the UI, and `Closes #$ARGUMENTS`.
- Report the PR link. **Don't merge unless the user asks.**
