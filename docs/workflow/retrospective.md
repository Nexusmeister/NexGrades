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
  (`.claude/skills/retrospective/SKILL.md`). That check is the authoritative one; the lead corrects the entry's
  "Repeat check" line in the next docs change, never in a PR of its own.

## Rules

- **No pupil data.** No names, grades, note text or record ids, in an entry or anywhere else in this file.
- **Entry format.** One entry per PR, newest first, under "Entries":

  ```markdown
  ### YYYY-MM-DD: PR #<pr> (issue #<n>) <short title>

  - **Cost and friction:** what went wrong or cost extra: blocked agents, retries, review findings a role should
    have caught, repeated fixes. Write "none" if nothing did.
  - **Lessons:** what to do differently, in one line each.
  - **Repeat check:** earlier entries with the same lesson (PR numbers), or "none". Filled in before the merge from
    the log as it is then; the lead's check after the merge is authoritative and corrects it in the next docs
    change.
  ```

- **Escalation.** A lesson that appears in two entries is a repeat. The lead then proposes an issue that changes
  an agent, a skill or a docs file, and opens it with the owner's OK. Nothing in `.claude/` or `AGENTS.md`
  changes without the owner's OK.

## Entries

### 2026-11-24: PR #219 (issue #210) Unlock dialog had no buttons

- **Cost and friction:** 4 role-agent runs (ui-designer on sonnet twice, reviewer on sonnet, docs-writer on haiku), and
  no architect, tester or csharp-coder run. The root cause was that the `ContentDialog` subclasses `UnlockYearDialog`
  and `ExportNotesDialog` did not get the WPF-UI implicit style, so they had no template and no button row. The first
  ui-designer run (sonnet) ran very long and was stopped by the usage limit without reporting; a second ui-designer
  run finished the work from the uncommitted state. The orchestrator ended once with a `BLOCKED` report and was
  resumed by the lead. Filing a follow-up issue through `issue_write` was denied by the permission classifier, so the
  follow-up was written as a PR comment instead. No CI fix round. The review had one round with no blockers or majors
  and two minors: `ExportNotesDialog` still hides its Export button while the range is invalid (the pattern this PR
  removed from the unlock dialog), and the regression test in `DialogChromeTests` does not cover the refusal path or
  the dark theme. The ui-designer should have checked sibling dialogs for the hidden-disabled-button pattern.
- **Lessons:**
  - A dialog button that a validation rule gates is disabled, not hidden; check sibling dialogs for the same pattern.
  - A regression test for a fix covers the refusal path and both themes, not only the happy path.
  - Hand back BLOCKED only when no role agent is still active, and judge waiting by the real clock (`date`) and file mtimes, not by round counts.
  - Background sleep rounds return much faster than their nominal time, so round counts understate the elapsed time.
- **Repeat check:** the background-wait lesson repeats PR #207 (the orchestrator ended before a background agent
  finished) and PR #196 (a BLOCKED hand-back judged by round count). That is a repeat; it goes to the lead under the
  Escalation rule. The hidden-button lesson is new.

### 2026-11-24: PR #216 (issue #215) Simplify dependabot.yml and releasing.md

- **Cost and friction:** no retries: no CI fix round. One review round (haiku reviewer): no blockers or majors, and
  three minors, filed as one follow-up issue. The yml was written by csharp-coder (sonnet) and the docs by
  docs-writer (haiku), run in parallel because the files did not overlap. The yml could only be checked for YAML
  syntax (PyYAML); Dependabot's own semantics are confirmed only on its first run on GitHub. Reviewer minors: the
  `velopack` label no longer lands on the Velopack package PR, the manual bump docs did not name the files, and one
  wording was imprecise. The docs-writer could have caught the file names with a fuller brief. Orchestrator: polling
  CI with several overlapping background sleeps produced many redundant notifications. Four role-agent runs plus this
  entry run.
- **Lessons:**
  - Check each label or group rule in a Dependabot config against the PRs it is meant to match, not only the YAML syntax.
  - Name the exact files in a docs brief, and have the docs step list the files it changed.
  - Poll CI with one sleep per round, not several overlapping background sleeps.
- **Repeat check:** none. The overlapping-sleep polling is new; the background role agent lesson (#198, #202, #205, #207)
  is not repeated here.

### 2026-11-24: PR #217 (issue #214) AsSplitQuery for Sonar S8733

- **Cost and friction:** none (one coder run, one opus review, no CI fix rounds, no review findings; no tester run
  because behaviour did not change).
- **Lessons:**
  - For a mechanical analyzer fix, skip architect and tester.
  - Give the coder the transaction facts up front so it needs no discovery.
- **Repeat check:** none.

### 2026-11-24: PR #211 (issues #204 and #203) Orchestrator workflow lessons

- **Cost and friction:** 6 role-agent runs: docs-writer on sonnet twice plus once for review fixes (three runs),
  reviewer on sonnet twice, docs-writer on haiku for this entry. No CI fix round and no denied actions. Round 1 of
  the review gave 1 major and 3 minors: the new rule "smoke start at the reported head SHA" conflicted with the
  retrospective entry commit that comes after it; the orchestrator should have checked the new rules against steps
  12 and 13 before the first review. Round 2 gave "Ready to merge" with three minors left (smoke-start skill intro
  does not mention the retrospective-only exemption, env vars removed instead of restored, notes drafted before the
  last CI waits). The docs-writer is slow on large multi-file briefs (about 15 minutes). The orchestrator stayed
  alive for every role agent through background sleep rounds, as the new rule says.
- **Lessons:**
  - When a PR adds a rule to the workflow files, check it against the later steps (12, 13, final report) before the
    first review.
  - Split a many-file docs brief into smaller briefs to cut wall time.
- **Repeat check:** none new; the background role agent lesson (#198, #202, #205, #207) is what this PR fixes.

### 2026-11-24: PR #212 (issues #206, #208) View-model load race guards

- **Cost and friction:** 5 role-agent runs (csharp-coder on sonnet twice, tester on sonnet, docs-writer on haiku,
  reviewer on sonnet), plus this entry on haiku. No CI fix round. The tester found a real bug in production code: a
  cancelled, superseded Classes load escaped as `TaskCanceledException` through the `[RelayCommand]` and would have
  crashed the app. It went to csharp-coder as a fix round, and the tests were then reworked to go through the real
  command path. The overlap guard already existed in `GradeSheetViewModel` and `StudentGradesViewModel`, but was
  missing in the Classes and Home view models; the review of #205 and #207 caught it only afterwards. The reviewer
  (sonnet) gave no blockers or majors and three minors, left unfiled: a stale failing run still logs a Warning; a
  pending "Not now" is lost if the winning refresh fails; and the Classes test passes through the cancel path rather
  than isolating the version guard. The coder and tester runs were slow, with many wait rounds.
- **Lessons:**
  - Every async load triggered by a property change or navigation needs a version counter (latest request wins) and a catch-and-log.
  - A `[RelayCommand]` with a `CancellationToken` must handle the cancellation of a superseded run.
  - Test the version guard in isolation; a test that passes through the cancel path does not prove it.
- **Repeat check:** the overlap guard lesson also appears in PR #205 (rapid toggle) and PR #207 (an action during an
  in-flight load). It is escalated as issue #209 (reference documentation), so not raised again here.

### 2026-11-24: PR #207 (issue #162, part 3 of 3) Home year-end reminder card

- **Cost and friction:** 10 role-agent runs (architect, csharp-coder twice, ui-designer, tester twice, docs-writer
  twice including this entry, reviewer twice), all on sonnet except docs-writer on haiku. The smoke start ran twice
  and passed both times. No CI fix round. The first review gave 1 major and 3 minors. The csharp-coder caught only
  `DbUpdateException` and `InvariantViolationException` on a read path, but a failed read throws `DbException` or
  `InvalidOperationException`; the reviewer found it, and the coder should have used the existing catch pattern for
  view-model loads. The second review gave "Ready to merge" with two minors left unfixed: a small race when "Not now"
  is pressed during the first in-flight refresh, and a culture-dependent date assertion in a test. The tester left a
  redundant `AddHours(0)`. The architect's plan was accurate; the orchestrator decided its open questions (past
  tense after the end date, reuse of the wizard button text). The orchestrator's first attempt ended early because it
  did not stay alive for the architect's background completion, and the lead resumed it (see #204).
- **Lessons:**
  - Brief csharp-coder to copy the existing catch pattern for view-model loads; a read can throw `DbException` or `InvalidOperationException`.
  - An action that can run during an in-flight load needs the same overlap guard as a rapid toggle.
  - Tests that assert a formatted date must fix the culture.
- **Repeat check:** background role agents, also in PR #198, #202 and #205; escalated as #204, so not raised again here.
  The overlap guard lesson also appears in PR #205 (a repeat; it goes to the lead under the Escalation rule).

### 2026-11-24: PR #205 (issue #162, part 2 of 3) Classes overview status toggle

- **Cost and friction:** 6 role-agent runs (csharp-coder, ui-designer and tester on sonnet, reviewer on sonnet,
  docs-writer on haiku twice), plus this entry run on haiku. No CI fix round and no denied actions. The review gave
  "Ready to merge" with no blockers or majors and three minors: a rapid toggle could start an overlapping reload,
  `StatusText` did not refresh on a language change, and the docs section mixed English and German labels. The
  ui-designer added one resource key (the status header) beyond its XAML-only scope; the coder left the XAML to the
  ui-designer on purpose. Role agents ran in the background again, although the brief said foreground, and were
  awaited through notifications. The first smoke-start log filter was buggy (PowerShell `TryParse` into a typed
  null) and was rerun with a fixed script. The lead corrected mid-run that the `dependabot.yml` check is a normal
  gate, not an exempt one.
- **Lessons:**
  - Role agents run in the foreground when the brief says so; this is the third occurrence, so escalate it.
  - A ui-designer brief must say whether resource keys are in scope; a key added outside that needs the coder's review.
  - A rapid input that starts an async reload needs an overlap guard, and view-model text must refresh on a language change.
  - Check a log filter against a known line before trusting an empty result from it.
  - Run docs-writer after the UI work, as PR #202 asked; this PR did.
- **Repeat check:** background role agents, also in PR #198 and PR #202 (escalation applies). The docs-writer ordering
  lesson from PR #202 was followed here, so it is not repeated.

### 2026-11-24: PR #202 (issue #162, part 1 of 3) Edit the Planned school year

- **Cost and friction:** 12 agent runs (architect opus 1, csharp-coder 1, ui-designer 1, tester 2, docs-writer 3,
  reviewer 2, plus one wasted haiku "wait" call). The docs-writer ran in parallel with the ui-designer and described
  the old XAML, so it had to run again. The XAML still bound a removed view-model property (`IsNextYearEditable`) and
  the build did not fail; the csharp-coder noticed it and the ui-designer removed the binding. The opus reviewer found
  two minors at the first commit; the fix (docs and a test) was reviewed only by haiku. Role agents again ran in the
  background although the brief said foreground. The smoke start ran on the tree before the first commit, and the lead
  re-ran it at the final head. The orchestrator was started before the retrospective step existed, so the lead added
  this entry.
- **Lessons:** run docs-writer after the UI work, not in parallel with it, when it documents the UI. A XAML binding to
  a missing property is a silent runtime failure, so a removed view-model property needs a XAML search. Run the smoke
  start at the head SHA that will be reported.
- **Repeat check:** role agents running in the background despite a foreground brief, also in PR #198. That is a
  repeat; it goes to the lead under the Escalation rule.

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
