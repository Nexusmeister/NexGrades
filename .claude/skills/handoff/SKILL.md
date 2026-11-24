---
name: handoff
description: Creates or updates the lead's local HANDOVER.md so a new lead session can continue the work. The lead uses it after every merge and at session end, and at about 90 % usage when the owner asks; the owner can type /handoff. Orchestrators and role agents don't use it.
---

# Handoff

Create or update the lead's local handoff doc, `HANDOVER.md` at the repository root, so a new lead session can
continue the work.

## Rules

- It is local. Never commit it.
- Read the existing file first, then rewrite the whole file. Never append.
- Keep it current after every merge.
- When the owner asks for it, stop work at about 90 % usage and write it.
- No pupil data: no names, grades or note text.

## Structure

Start with a short header: the date, the session, and how to pick up. To pick up, a new lead session reads the
file whole, then `AGENTS.md`, `CLAUDE.md`, `docs/domain/README.md`, the memory index and the lead skills
`implement-issue`, `verify-and-merge`, `smoke-start`, `retrospective` and `handoff`. The owner may ask to delete the file after it
has been read.

Then use these sections, in this order:

1. **Goal**: the current focus.
2. **In-flight issues and worktrees**: each issue number, its worktree path and the orchestrator's state.
3. **PRs merged**: PR number, title and merge commit.
4. **Follow-ups filed**: issue numbers with a short title.
5. **Binding decisions**: decisions by the owner or the lead that later work must follow.
6. **What worked**: approaches to reuse.
7. **What didn't work**: failed approaches and pitfalls.
8. **Next steps**: what the next session does first.
