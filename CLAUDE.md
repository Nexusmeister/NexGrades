# CLAUDE.md

Project conventions live in `AGENTS.md` so every AI tool shares them. Edit that file, not this one, when
a convention changes.

@AGENTS.md

## Claude Code specifics

- Shared permissions are in `.claude/settings.json`; put personal overrides in
  `.claude/settings.local.json` (git-ignored).
- The development machine is Windows. Commands in `AGENTS.md` work in both PowerShell and Git Bash; run
  them from `src/NexGrades/`.
- To work an issue end to end, run `/implement-issue <number>` in the main session. It is the lead's front
  door: it reads the issue, settles open questions and briefs an `issue-orchestrator` agent. The orchestrator
  works in its own worktree (`.claude/worktrees/issue-<n>`) and runs the role agents in `.claude/agents/`
  (nested subagents are supported; if nesting fails, the skill runs it as a background session with
  `claude --agent issue-orchestrator --bg "<brief>"`). It stops at merge-ready. The lead then runs the
  `verify-and-merge` skill and merges.
- Delegate to a subagent when its role fits:
  - `issue-orchestrator`: runs one issue end to end through the roles below; never edits, never merges
  - `architect`: design a change before code is written
  - `csharp-coder`: Domain, Data and view-model code
  - `ui-designer`: XAML pages and layout
  - `tester`: tests
  - `reviewer`: review a diff before a PR
  - `docs-writer`: README and XML docs
- An agent file merged while a session runs does not appear in that session's Agent tool
  (`Agent type ... not found`) until you run `/reload-skills` or start a new session.
- Use `/grill-me` when requirements are unclear, before planning.
