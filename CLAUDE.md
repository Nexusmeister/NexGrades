# CLAUDE.md

Project conventions live in `AGENTS.md` so every AI tool shares them. Edit that file, not this one, when
a convention changes.

@AGENTS.md

## Claude Code specifics

- Shared permissions are in `.claude/settings.json`; put personal overrides in
  `.claude/settings.local.json` (git-ignored).
- The development machine is Windows. Commands in `AGENTS.md` work in both PowerShell and Git Bash; run
  them from `src/NexGrades/`.
- To work an issue end to end, run `/implement-issue <number>`. It drives the subagents in
  `.claude/agents/` from the main session. Alternatively, hand the issue to the `issue-orchestrator`
  agent, which runs it end to end through the role agents (nested subagents are supported; if nesting
  fails, run the orchestrator as a background session (`claude --agent issue-orchestrator --bg "<brief>"`))
  and stops at merge-ready. The main session (the lead) merges.
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
