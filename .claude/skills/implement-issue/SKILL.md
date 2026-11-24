---
name: implement-issue
description: The lead's front door for one NexGrades GitHub issue. Reads the issue, settles open questions, hands a complete brief to the issue-orchestrator agent, then verifies and merges. Use when the user asks to implement, work on or pick up an issue by number.
argument-hint: <issue-number>
disable-model-invocation: true
---

# Implement issue #$ARGUMENTS

You are the lead. This skill is the front door for one orchestrated issue: you prepare the brief, start one
`issue-orchestrator` for the issue, and verify and merge its result. The orchestrator runs the pipeline (worktree,
plan, implementation, tests, docs, PR, CI, review) through the role agents in `.claude/agents/`. Read `AGENTS.md`
first if it is not already in context.

The `gh` CLI is not installed on the dev machine; use the GitHub MCP tools (`mcp__github__issue_read` and so on).

Work goes through the role agents: `architect`, `csharp-coder`, `ui-designer`, `tester`, `docs-writer`, `reviewer`.
A `general-purpose` agent never writes code.

Keep the user informed with one short line per step.

## 1. Understand the issue

- Read issue $ARGUMENTS and its comments with `mcp__github__issue_read`.
- If the goal, scope or acceptance criteria are unclear, run the `grill-me` skill with the user before
  starting the orchestrator. Don't hand over guesses.

## 2. Build the brief

The orchestrator sees only the brief. Fill every field of this template (the fields are the ones listed under
"Input from the lead" in `.claude/agents/issue-orchestrator.md`):

```text
Issue: #$ARGUMENTS
Main checkout (absolute path): <path>
Base branch: dev
Constraints: <dependencies on other issues, "runs alone", files to avoid, or "none">
Binding decisions: <decisions already made, or "none">
Commit attribution lines: <verbatim lines>
PR footer lines: <verbatim lines>
Model overrides: <optional, or "none">
```

If the commit attribution lines or the PR footer lines are missing, the orchestrator stops with `BLOCKED`.

The brief carries only the issue-specific facts from the template. Don't repeat what the agent file covers: role
agents returning asynchronously and the waiting rules, `docs-writer` after `ui-designer`, the XAML binding search,
the smoke start at the reported head SHA and the CI wait rules.

## 3. Launch the orchestrator

- Start the `issue-orchestrator` agent with the brief and pass `model` explicitly on every Agent call.
- One orchestrator per issue; it works in its own worktree (`.claude/worktrees/issue-<n>`).
- If it reports `BLOCKED: nesting unavailable`, run it as a background session instead:

  ```sh
  claude --agent issue-orchestrator --bg "<brief>"
  ```

- The orchestrator's steps 6 and 7 (verification, smoke start) and its CI wait rules in
  `.claude/agents/issue-orchestrator.md` define how the result is verified. Don't copy or weaken them here.

## 4. Verify and merge

When the orchestrator reports `MERGE-READY: PR #<n> @ <head sha>`, read and follow `.claude/skills/verify-and-merge/SKILL.md`. On
`BLOCKED: <reason>`, deal with the reason (ask the owner where a decision is needed) before starting again.
