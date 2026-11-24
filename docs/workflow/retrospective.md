# Workflow retrospective

A running log of what the agent workflow (lead, `issue-orchestrator` and the role agents) learned from each
merged pull request. Its purpose is to find repeated friction and fix it in the agents, skills or docs, instead
of paying for the same mistake again.

The lead writes one entry after every merge, with the `retrospective` skill
(`.claude/skills/retrospective/SKILL.md`).

## Rules

- **No pupil data.** No names, grades, note text or record ids, in an entry or anywhere else in this file.
- **Entry format.** One entry per merged PR, newest first, under "Entries":

  ```markdown
  ### YYYY-MM-DD: PR #<pr> (issue #<n>) <short title>

  - **Cost and friction:** what went wrong or cost extra: blocked agents, retries, review findings a role should
    have caught, repeated fixes. Write "none" if nothing did.
  - **Lessons:** what to do differently, in one line each.
  - **Repeat check:** earlier entries with the same lesson (PR numbers), or "none".
  ```

- **Escalation.** When a lesson repeats, for example when it appears in two entries, the lead opens an issue that
  proposes a change to an agent, a skill or a docs file. Nothing in `.claude/` or `AGENTS.md` changes without the
  owner's OK.

## Entries

<!-- Newest first. No entries yet. -->
