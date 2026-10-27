# CLAUDE.md

Project conventions live in `AGENTS.md` so every AI tool shares them. Edit that file, not this one, when
a convention changes.

@AGENTS.md

## Claude Code specifics

- Shared permissions are in `.claude/settings.json`; put personal overrides in
  `.claude/settings.local.json` (git-ignored).
- The development machine is Windows. Commands in `AGENTS.md` work in both PowerShell and Git Bash; run
  them from `src/NexGrades/`.
- Subagents (`.claude/agents/`) and skills (`.claude/skills/`) for this repo are planned in issue #45.
