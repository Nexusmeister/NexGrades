---
name: lead-handover
description: Writes or refreshes the lead's local HANDOVER.md so a new lead session can continue the work. Use after every merge. The handover is written when the session ends and, when the owner has asked for it, at about 90 % usage.
disable-model-invocation: true
---

# Lead handover

`HANDOVER.md` at the repository root is the lead's notes for the next session.

## Rules

- It is local. Never commit it.
- Rewrite the whole file each time. Never append.
- Keep it current after every merge.
- When the owner asks for it, stop work at about 90 % usage and write the handover.
- No pupil data: no names, grades or note text.

## Sections

Use these sections, in this order:

1. **In-flight issues and worktrees**: each issue number, its worktree path and the orchestrator's state.
2. **PRs merged**: PR number, title and merge commit.
3. **Follow-ups filed**: issue numbers with a short title.
4. **Binding decisions taken**: decisions by the owner or the lead that later work must follow.
5. **Next steps**: what the next session does first.
