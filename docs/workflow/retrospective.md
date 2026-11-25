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

### 2026-11-25: PR #250 (issue #247) Line-wrap rule for the docs-writer role

- **Cost and friction:** no retries, no CI fix rounds, no blocked steps. One review round (reviewer, sonnet) gave two
  minors (the "about 120" versus "paragraph width" wording; a vague line-length check) and "Ready to merge" at
  a6e9af8; both minors left as is. docs-writer (haiku) made the edit and applied its own new rule. Three role-agent
  runs (docs-writer haiku twice, reviewer sonnet once); no architect, coder, ui-designer or tester. Open decision for
  the owner: whether the brief template in `.claude/skills/implement-issue/SKILL.md` should mention the wrap rule.
  Several orchestrator background sleeps returned instantly, so waits relied on notifications.
- **Lessons:**
  - The wrap rule now lives in `docs-writer.md`; watch whether the extra re-wrap round disappears.
- **Repeat check:** PR #246 and PR #229 (docs-writer left lines over the wrap width); this PR is the fix.

### 2026-11-25: PR #249 (issue #248) Let the lead invoke implement-issue and verify-and-merge

- **Cost and friction:** the lead could not start `implement-issue` for #247, because the skill's
  `disable-model-invocation: true` refused the Skill tool, and the lead had to ask the owner to type the
  command. The owner wanted the lead to call the skill itself. The same flag was on `verify-and-merge`.
  The lead made the two-line change directly, without an orchestrator; one review round (reviewer, sonnet);
  docs-writer (haiku) wrote this entry.
- **Lessons:**
  - Check a skill's front matter for `disable-model-invocation` before planning to call it, and ask the owner
    early if it blocks the plan.
- **Repeat check:** none.

### 2026-11-25: PR #246 (issue #244) Aspire comment and docs minors from the #239 review

- **Cost and friction:** no retries, no CI fix round and no blocked steps. One review round (reviewer, sonnet)
  gave no findings and "Ready to merge" at 41ae703. The docs-writer (sonnet) left two lines over the wrap width
  (a CONTRIBUTING.md bullet and a sentence in this file); the orchestrator caught them in the diff, and a
  docs-writer (haiku) re-wrapped them. Role-agent runs: four after the PR was opened (docs-writer sonnet once,
  docs-writer haiku twice, reviewer sonnet once) and four before it. No architect, csharp-coder, ui-designer
  or tester ran. Several orchestrator background sleeps returned instantly, so the waits relied on agent
  notifications.
- **Lessons:**
  - Give docs-writer the exact wrap width up front and check each changed line's length before handing back.
  - Check a wording an issue suggests against the tool's real behaviour before writing it, and drop it if it
    does not apply (here the nuget-minor-patch group never reaches the three ignored packages).
  - Judge waits by `date` and check-run times, not by the number of sleep rounds.
- **Repeat check:** the wrap-width lesson repeats PR #229 (the docs brief needs the line limit), so it goes
  to the lead under the Escalation rule. The wait lesson repeats PR #196, #219, #226, #233, #234, #238, #239
  and #240. The wording-check lesson is new; it is related to PR #226 (docs steps check their references), but
  it is about external package behaviour. This entry also corrects the PR #239 entry's repeat check.

### 2026-11-25: PR #245 (issue #243) Tester role: mutation check and App.Tests scope

- **Cost and friction:** none. Single docs-writer run (sonnet) for the agent file, one reviewer run (sonnet); no CI or review fix rounds. Reviewer agreed that reviewer.md stays unchanged.
- **Lessons:** A guard test must fail with the guard removed (mutation check); the tester now does this and reports it. The tester's scope now includes NexGrades.App.Tests, so the lead no longer needs a scope note in the brief.
- **Repeat check:** none (this PR is the escalation of the repeat in PR #212 and PR #238).

### 2026-11-25: PR #240 (issue #194) Localization nits

- **Cost and friction:** 4 role-agent runs: csharp-coder (sonnet), tester (sonnet), reviewer (sonnet) and docs-writer
  (haiku); no architect. No CI fix round and no denied actions. One review round gave no blockers or majors and two
  minors: a sibling German tooltip still had the old wording (filed as a follow-up issue), and no tests pin the
  resource strings of items 3 and 6. Nothing in the review was a blocker that a role should have caught. The
  orchestrator reported the csharp-coder as BLOCKED after about 2.5 hours, because it counted background sleep rounds
  as elapsed time. Real time (`date`) showed about 12 minutes, and the lead corrected the report.
- **Lessons:**
  - Judge a stalled agent by `date` and file modification times, never by counting sleep rounds.
  - When a wording changes, search every language's resource file and the XAML for the old text, so no sibling is left behind.
  - Name in the tester brief each changed resource string that needs a test, or say why none is pinned.
- **Repeat check:** the first lesson repeats PR #226, PR #219 and PR #196 (a BLOCKED report judged by round count); it
  goes to the lead under the Escalation rule. The sibling-text lesson is related to PR #219 (sibling dialogs) but is not
  the same lesson. The tester-brief lesson is new.

### 2026-11-25: PR #239 (issue #225) Aspire upgrade to 17.0.0

- **Cost and friction:** no CI fix round. Wait rounds for CI were judged by counted sleep rounds, which returned almost
  instantly; the orchestrator wrongly reported `BLOCKED: CI timeout` while the real clock had advanced only minutes.
  Review: opus reviewer gave "Ready to merge" with four minors, all fixed in a docs/config commit and re-reviewed by
  haiku (ready to merge). Deviation: `Aspire.AppHost.Sdk` 17.0.0 is obsolete (ASPIRE012 error), so the AppHost moved to
  `Microsoft.NET.Sdk` with explicit package references and no `ProjectReference` to the App; this was found only at
  build time. The AGENTS.md layout table cell for the AppHost had to be corrected, and the reviewer found a wrong
  "no minimum" comment on the UserSecrets pin that the coder had written. Other review findings per role: the
  csharp-coder wrote an inaccurate Dependabot reason comment; the docs-writer left CONTRIBUTING.md saying the Aspire SDK
  comes from NuGet, which the reviewer caught. Agent runs: csharp-coder (sonnet) 1, reviewer opus 1 and haiku 1,
  docs-writer haiku 3 (including this entry). The coder run was slow because builds competed with other worktrees.
  Lead verification: the orchestrator's final report never reached the lead, so the lead merged `origin/dev`
  (retrospective conflict only, resolved by docs-writer on haiku) and ran a new opus review at the head, which found
  no blockers or majors (2 minors and 3 nits, filed as #244). The PR body said the AppHost trial-run processes were
  stopped, but the AppHost, its DCP processes and a SQLite Web Docker container kept running for about 4.5 hours.
  They locked the AppHost build output in the worktree until the lead stopped them with the owner's OK.
- **Lessons:**
  - Wait on a real deadline (a loop on `date` or epoch seconds against the check runs), never by counting sleep rounds.
  - Check a pinned SDK or package for obsolescence (for example a diagnostic such as ASPIRE012) before briefing a coder.
  - Have the docs step search the whole repository for statements the change makes false, not only the files it names.
  - Have the coder check its own comments (version minimums, reasons) against the source before handing back.
  - Don't run builds for several worktrees at the same time; they compete for the same resources and slow every run.
  - An agent that starts the AppHost stops the whole process tree (including DCP and any containers) and checks with
    a process list that nothing from the worktree is still running before it reports.
- **Repeat check:** the background-wait lesson repeats PR #196, #207, #219, #226, #233 and #234; it is escalated to the
  lead under the Escalation rule. The docs-miss lesson is related to PR #226 (a docs step that did not check its code
  references), but it is not the same miss. The parallel-build lesson is related to PR #228 and PR #235 (builds in the
  same worktree, handled by PR #237); builds in separate worktrees slowing each other down is new.

### 2026-11-25: PR #238 (issue #236) Test that a superseded Classes overview load never sets HasLoaded

- **Cost and friction:** about 5 role-agent runs: tester (sonnet) twice, reviewer (haiku) twice and docs-writer
  (haiku) once. The orchestrator mistook fast-returning background sleep rounds for elapsed CI time and reported a
  false "CI timeout"; the lead corrected it. The first version of the two tests passed even with the production
  version guard removed: the gate held the years read, so the superseded run was cancelled by the newer command run
  and threw `OperationCanceledException` before it reached the guard. The haiku reviewer caught this as a major
  (unconfirmed), and a mutation check by the tester confirmed it. The tests were reworked to gate on the class query
  and to start the superseded run with an uncancelled token, via reflection. The tester should have run that mutation
  check itself before reporting the tests as done. One review round with a major, then one re-review: "Ready to
  merge". The tester role is limited to `NexGrades.Tests`, so the lead's explicit scope note was needed for
  `NexGrades.App.Tests`.
- **Lessons:**
  - A new test for a guard or branch is not done until a mutation check (remove the guard, the test must fail) has
    run; put this in the tester brief.
  - Count CI wait rounds against a real clock (loop on `date` with a deadline and poll the check-runs API), never by
    the number of sleep calls.
  - For latest-request-wins loads, a test that goes through the command cancels the older run first; reach the guard
    by starting the older run with an uncancelled token.
  - Name the test project in the tester brief when it is outside `NexGrades.Tests`.
- **Repeat check:** the CI-clock lesson repeats PR #196, PR #219, PR #226, PR #233 and PR #234, so it goes to the lead
  under the Escalation rule. The guard-isolation lesson repeats PR #212. The mutation-check and project-scope lessons
  are new.

### 2026-11-25: PR #237 (issue #224) Orchestrator waits by the real clock, one builder per worktree

- **Cost and friction:** none blocking: no retries, no blocked agents and no CI fix round. One review round (sonnet
  reviewer) gave "Ready to merge" with three minor clarity findings, left as they were: the loop deadline and the
  `started_at` source are not the same clock, one sentence is duplicated, and one worktree rule is repeated. Three
  role-agent runs: docs-writer (haiku) for the two agent and skill file edits and for this entry, and reviewer
  (sonnet). No architect, csharp-coder or tester. The sandbox clock barely moved during the run: `date` advanced by
  seconds while waits of many minutes were requested, so counted sleep rounds are no measure of elapsed time. No App
  code changed, so no smoke start. The lead's check showed the clock was correct: the background sleeps returned almost at once, so the waits were short, not frozen.
- **Lessons:**
  - Judge a CI timeout by `date` against the run's `started_at`, never by the count of sleep rounds or the requested
    wait time.
  - Give each loop deadline one clock source, and check a workflow file for duplicated sentences before the review.
- **Repeat check:** the round-count lesson repeats PR #234, PR #228, PR #219, PR #207 and PR #196; it goes to the lead
  under the Escalation rule. The duplicate-sentence lesson is new.

### 2026-11-25: PR #235 (issue #178) Empty state on the overview pages only after the first load

- **Cost and friction:** 7 role-agent runs: csharp-coder twice, ui-designer, tester, reviewer (all sonnet), and
  docs-writer twice on haiku (once for the retrospective entry). No architect and no CI fix round. The first clean,
  non-incremental build failed on MA0051 (the Classes load method was 66 lines, limit 60); an earlier incremental build
  had hidden it, so an extra csharp-coder run extracted `ApplyLoad`. The ui-designer ran while the csharp-coder was
  still editing the same file, saw a half-edited file and reported a false build break; the orchestrator verified the
  tree with its own `--no-incremental` build. The docs-writer's late edit needed a second docs commit. One review
  round gave "Ready to merge" with no blockers or majors and two minors; the first (a failed load shows the empty
  state) is deliberate. After a lead session change the reviewed SHA was not recorded in the handover, so the lead
  ran a second review (sonnet) at the head `46a9731`: Ready to merge, no blockers or majors, four minors (an untested
  superseded-load case for Classes became follow-up issue #236; the others need no action). The lead's own
  `-warnaserror` build, started while that reviewer built in the same worktree, reported one transient error; a
  rebuild afterwards had 0 warnings and 0 errors. Lead smoke start pass at `46a9731`. Role-agent results arrived
  with long delays, but the notes do not say whether the runs were in the background.
- **Lessons:**
  - Check a C# change with a non-incremental build (`dotnet build --no-incremental`) before handing back; an
    incremental build can hide analyzer failures such as MA0051.
  - Run each role agent to completion before the next one reads the same worktree, and let the orchestrator confirm
    the build itself.
  - Run docs-writer to completion before the docs commit, so its late edit does not need a second commit.
  - Record the reviewed SHA and the smoke-start SHA in the handover, so a new lead session need not repeat the review.
  - Don't build in a worktree while another agent builds there; parallel builds lock files and report false errors.
- **Repeat check:** the concurrent-worktree lesson repeats PR #228 (ui-designer and tester in the same solution). The
  docs-writer timing lesson repeats PR #202 and PR #232, so it goes to the lead under the Escalation rule. The
  analyzer-before-handback lesson is new. The parallel-build lesson repeats this PR's own
  ui-designer/csharp-coder overlap and PR #228. The handover-SHA lesson is new.

### 2026-11-25: PR #234 (issues #171, #172, #193, #197) Docs and config minors

- **Cost and friction:** 3 role-agent runs: docs-writer (haiku) for the edits, reviewer (haiku), docs-writer (haiku) for this entry; no architect, coder or tester. No CI fix round, one review round with two minor findings (a version number in a comment for an unpinned transitive package that can go stale; a commit trailer model name, which was the lead's verbatim attribution line and was kept). No smoke start (no App code). Issue #172 had to be adjusted by the lead because PR #223 had made its premise stale; the orchestrator verified TFMs in the local NuGet cache instead of trusting the issue. The #171 CONTRIBUTING.md line was already pinned, so only the update alternative was added.
- **Lessons:**
  - Re-check bundled follow-up issues against current dev before briefing; one of four was already partly done or stale.
  - The orchestrator's wait rounds do not advance wall-clock time visibly; judge CI by started_at and check runs, not by the number of sleeps.
- **Repeat check:** none.

### 2026-11-25: PR #233 (issues #164 and #166) Doc fixes and too-long class name hint in the rollover dialog

- **Cost and friction:** 5 role-agent runs, all on sonnet except docs-writer on haiku: csharp-coder, ui-designer,
  tester, reviewer and docs-writer. No architect was needed. No CI fix round. One review round gave no blockers or
  majors and three minors, none needing action. The csharp-coder deviated from the brief on purpose: the brief asked
  it to reuse the `Classes_` resource key, but the localization rules require area-prefixed keys, so it added a new
  key with the same text in both languages. Background waiting was slow because the completion notifications of
  sleep rounds arrived late.
- **Lessons:**
  - Check a brief's resource key reuse against the key-naming rule in the UI text and localization page before
    sending the brief.
  - Judge background waiting by `date` and file modification times, not by the number of sleep rounds.
- **Repeat check:** the background-wait lesson repeats PR #228, PR #219, PR #207 and PR #196. The key-reuse lesson is
  new; it is related to PR #205 (say in the ui-designer brief whether resource keys are in scope), but it is not the
  same lesson.

### 2026-11-25: PR #232 (issue #227) Classes year filter follow-ups

- **Cost and friction:** 7 role-agent runs: csharp-coder (sonnet), tester (sonnet), docs-writer (haiku) three times,
  reviewer (sonnet); no architect. No CI fix round. One review round with no findings. No smoke start (tests, Domain
  and docs only). The first docs-writer run ran in parallel with the csharp-coder, so its docs said the order of equal
  start dates was undefined while the code added an `Id` tiebreaker; a second run fixed it. Two further docs-writer
  runs fixed a dropped word and an extra blank line at the end of `releasing.md`.
- **Lessons:**
  - Run docs-writer after the csharp-coder when the docs describe the behaviour the coder is changing.
  - Brief docs-writer on CRLF files with explicit end-of-file expectations.
- **Repeat check:** PR #202 (docs-writer run in parallel with the code it documented). PR #205 followed the lesson; this
  PR repeats it, so it goes to the lead under the Escalation rule. The CRLF lesson is new.

### 2026-11-25: PR #230 (issue #209) Guidance for async loads in view models

- **Cost and friction:** 3 role-agent runs (docs-writer on sonnet, reviewer on sonnet, docs-writer on haiku). No retries
  and no CI fix round. One review round gave no blockers or majors and two minors, both filed as a follow-up issue
  instead of fixed, to avoid another review pass: a checklist rule is worded stricter than `GradeSheetViewModel` follows
  (`YearLock.ShowAsync` is awaited without a version re-check), and a testing bullet claims a message assertion that the
  Classes test does not make. The docs-writer (sonnet) wrote the page and the two agent-file lines in one run, after
  reading the four view models and their tests, and noted the Warning vs Error log levels itself. Docs-only: no App
  change, so the smoke start does not apply. The PR needs the owner's OK because it changes `.claude/agents`.
- **Lessons:**
  - Check each rule in a docs page against the code it cites before the first review; a rule stricter than the reference code costs a review round.
  - Check each test claim in a docs page against the test it names before the first review.
  - A change to `.claude/agents` waits for the owner's OK before it merges.
- **Repeat check:** the docs-matches-code lesson repeats PR #226 (have the docs step check its code references), so it
  goes to the lead under the Escalation rule. The owner's OK lesson is new. The lead's `retrospective` skill check after
  the merge is authoritative.

### 2026-11-25: PR #229 (issue #218) Clarify Velopack/vpk bump, labels and grouping in dependabot docs

- **Cost and friction:** 5 role-agent runs on 2 models: csharp-coder (sonnet) for the yml comments, docs-writer (haiku)
  three times (for releasing.md, for the post-review wording fix made with this entry, and for a count correction),
  reviewer (haiku) once. No CI fix round, no denied actions. The reviewer gave "Ready to merge" with two minors in the
  docs-writer's wording (a 121-character line, and "one grouped pull request" wrong for NuGet, which has two groups);
  both fixed in the PR. The csharp-coder and docs-writer ran in parallel on different files without conflict. Sleep
  rounds returned instantly, so the wait for CI and the reviewer was spent on many short polls.
- **Lessons:**
  - When a docs sentence states a count (one group, one PR), check it against the config it describes.
  - Give docs-writer the exact line limit up front.
- **Repeat check:** none.

### 2026-11-24: PR #228 (issue #220) Notes export dialog keeps Export visible

- **Cost and friction:** 3 role-agent runs plus the review (ui-designer sonnet, tester sonnet, reviewer sonnet); no
  architect, and no docs-writer run was needed for the code. No CI fix round (CI result is recorded in the PR). One
  review round with no blockers or majors and two minors: `TryCreateRequest` raised `PropertyChanged` through
  `Revalidate`, and the click test relied on WPF-UI's button command wiring. The ui-designer's solution build hit a
  VSTHRD002 error in the tester's still-in-progress test file, because both agents worked in the same worktree at
  once; the tester fixed it. The tester could not assert on the `ShowAsync` task (a dispatcher frame made other tests
  flaky) and watched the synchronous `Closing` event instead. Background wait rounds again returned almost instantly;
  waiting was judged by file mtimes and running test hosts.
- **Lessons:**
  - Run ui-designer and tester one after the other when the tester's files are built in the same solution, or expect a
    transient build error from half-written tests.
  - Test a refused dialog click through the dialog's `Closing` event, not the `ShowAsync` task, in the shared STA test
    setup.
  - The fix for a sibling dialog was a copy of the #219 pattern: check the reference PR's follow-up list first.
- **Repeat check:** the background-wait lesson repeats PR #219, PR #207 and PR #196; the rest is new.

### 2026-11-24: PR #226 (issue #213) Classes overview: school year filter

- **Cost and friction:** 6 role-agent runs: csharp-coder, ui-designer, tester and reviewer on sonnet, and docs-writer
  on haiku twice. No CI fix round and no denied actions. One review round gave "Ready to merge" with no blockers or
  majors and four minors. Three of them: the docs "Code" list misses `SchoolYearService`, `GetYearsAsync` orders
  without a tiebreaker, and the fallback when the selected year disappears is untested. The csharp-coder also flagged
  that the class page has no `YearLock` bar, so the issue's "read-only for Closed years" does not hold today. This is
  documented; no lock logic was added. The orchestrator misjudged elapsed time by counting short sleep rounds and
  reported a stalled csharp-coder as BLOCKED; the lead corrected it by comparing `date` with file modification times.
  The smoke start passed at 1f3e9f3.
- **Lessons:**
  - Judge a stalled agent by `date` and file modification times, never by counting sleep rounds.
  - Before coding a lock-related issue, check that the page has the shared `YearLockBar`; a missing bar is a gap to
    document or file, not one to patch ad hoc.
  - Give the tester the fallback path (the selected item disappears) and the ordering tiebreaker as explicit cases.
  - Have the docs step check its code references against the services the change touches.
- **Repeat check:** none. The docs miss is related to the docs-brief lesson in PR #215, but it is a different miss.

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

### 2026-11-24: PR #217 (issue #214) AsSplitQuery for Sonar S8733

- **Cost and friction:** none (one coder run, one opus review, no CI fix rounds, no review findings; no tester run
  because behaviour did not change).
- **Lessons:**
  - For a mechanical analyzer fix, skip architect and tester.
  - Give the coder the transaction facts up front so it needs no discovery.
- **Repeat check:** none.

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

### 2026-11-24: PR #196 (issue #169) Handoff skill

- **Cost and friction:** the orchestrator reported `BLOCKED: CI timeout` after 30 rounds on a Markdown-only PR.
  CI was only slow and finished green; the lead re-checked and merged. The reviewer (haiku) found 2 minors:
  `HANDOVER.md` was not in `.gitignore` (filed as #197).
- **Lessons:** slow CI is not a failure; the lead re-checks the check runs before acting on a timeout.
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
