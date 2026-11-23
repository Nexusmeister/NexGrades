# AGENTS.md

Instructions for AI coding agents working in this repository. This file is the single source of truth
for project conventions; tool-specific files (e.g. `CLAUDE.md`) import or point to it rather than
duplicating it. Keep it current when a convention changes.

## What NexGrades is

A Windows desktop grade book for a teacher: school years, classes, students, subjects, weighted grade
buckets and averages. Single user, local SQLite database, no server.

## Stack

- .NET 11 (`net11.0`, App is `net11.0-windows`), C# with nullable reference types and implicit usings
- WPF with [WPF-UI](https://wpfui.lepo.co/) 4.x (Fluent controls, navigation, snackbar, theming)
- CommunityToolkit.Mvvm 8.x (`[ObservableProperty]`, `[RelayCommand]`)
- EF Core 11 on SQLite
- .NET Aspire AppHost for local development only
- Velopack 1.x for installing, updating and packing the app (`Directory.Packages.props`); the `vpk` tool in `.config/dotnet-tools.json` has the same version
- xUnit for tests

## Repository layout

All code lives under `src/NexGrades/` (solution: `NexGrades.slnx`).

| Project | Role | References |
|---|---|---|
| `NexGrades.App` | WPF shell, pages, view models, DI wiring, host setup, file logging (`Logging/`), self-update (`Updates/`) | Common, Data, Domain, ServiceDefaults |
| `NexGrades.Domain` | Business rules: services, grading, closed-year auditing, `IClock` | Data |
| `NexGrades.Data` | EF Core `AppDbContext`, entities, entity configurations, migrations | — |
| `NexGrades.Common` | Small cross-cutting helpers and service interfaces | — |
| `NexGrades.Tests` | xUnit tests for Domain and Data | Data, Domain |
| `NexGrades.App.Tests` | xUnit tests for the App's DI composition and XAML loading (Windows only) | App |
| `Aspire/NexGrades.AppHost` | Dev-time orchestration (SQLite + SQLite Web) | App |
| `Aspire/NexGrades.ServiceDefaults` | OpenTelemetry logs, metrics and traces, used by App; exports only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set | — |

Dependency direction is App → Domain → Data. Data must not reference Domain or App; Domain must not
reference App or WPF. ServiceDefaults is referenced only by App and must not reference any app project.

## Commands

Run from `src/NexGrades/`:

```sh
dotnet restore
dotnet build NexGrades.App/NexGrades.App.csproj
dotnet build NexGrades.slnx -warnaserror                             # build every project with warnings as errors
dotnet test
dotnet format NexGrades.slnx                                         # apply the .editorconfig style and whitespace rules
dotnet ef migrations add <MigrationName> --project NexGrades.Data --startup-project NexGrades.Data   # needs the dotnet-ef global tool
dotnet run --project Aspire/NexGrades.AppHost                        # run the app for development
```

- The SDK is pinned by `global.json` at the repository root (11.0.100 or a later 11.0 feature band). CI installs the
  SDK from that file.
- **Zero warnings:** the build must produce no warnings. GitHub Actions sets `CI=true`, which makes
  `Directory.Build.props` set `TreatWarningsAsErrors`, and the workflow also passes `-warnaserror` explicitly to
  `dotnet build` and `dotnet test`, so any warning fails CI. CI also runs `dotnet format NexGrades.slnx
  --verify-no-changes`, so unformatted code fails too. Local builds stay non-fatal; reproduce the CI checks with
  `dotnet build NexGrades.slnx -warnaserror` and `dotnet format NexGrades.slnx --verify-no-changes`.
- In `Development`, the App needs `ConnectionStrings:sqlite`: the Aspire AppHost supplies it, and running
  `NexGrades.App` on its own needs it from user secrets. Database location, legacy relocation and log files: see
  [startup, logging and data paths](docs/domain/startup-logging-and-data-paths.md).
- To see logs, metrics and traces live, run the AppHost command above and open the Aspire dashboard at the URL it
  prints. See `docs/decisions/0002-telemetry-and-privacy.md`.
- The App is a GUI and cannot be meaningfully exercised headless. Verify changes with a build of
  `NexGrades.App` plus `dotnet test`; say so explicitly when a change also needs a manual UI check.
- **Release assets:** from the repository root, `./.github/scripts/New-ReleaseAsset.ps1 -OutputDirectory <dir> -Channel beta`
  (or `-Channel stable`) publishes the App self-contained for win-x64 and packs it with `vpk` into the installer, the
  portable zip and the update feed of that channel, in `<dir>\release`. The version is the one MinVer gives HEAD: the
  beta channel takes `X.Y.Z-beta.N`, the stable channel `X.Y.Z`. `<dir>` is emptied if it exists. Pull request builds
  run the beta pack without uploading anything. See `docs/releasing.md`.

## Domain model

Before changing code, data or behaviour in an area listed in [`docs/domain/README.md`](docs/domain/README.md),
read that area's page first; it explains the entities, behaviour and reasons that the rules below summarise.

Read the XML doc comments on the entities in `NexGrades.Data/Entities/` before changing the model —
they record *why* the model is shaped the way it is.

Entities: SchoolYear, ClassGroup, ClassInstance, Student, Enrollment, Subject, BucketTemplate, SubjectInstance,
GradeBucket, Assessment, Grade, FinalGrade, SubjectParticipation, Note, Teacher, AuditEntry; the
[glossary](docs/domain/README.md) describes each.

### Invariants agents must not break

- **Nothing is ever deleted.** Entities change status instead. `ClosedYearAuditInterceptor` rejects every
  `Deleted` entry. See [Closed-year lock and audit](docs/domain/closed-year-lock-and-audit.md).
- **The school year is derived by traversal** (e.g. Grade → GradeBucket → SubjectInstance → ClassInstance →
  SchoolYear, and Assessment → GradeBucket → SubjectInstance → ClassInstance → SchoolYear, and FinalGrade → SubjectInstance → ClassInstance → SchoolYear). Do not add denormalized
  `SchoolYearId` columns; only SchoolYear, ClassInstance and Note carry one. See [Closed-year lock and audit](docs/domain/closed-year-lock-and-audit.md).
- **Closed years are locked.** Writes to a Closed year throw `ClosedYearException` unless the year is
  unlocked, and every allowed write produces `AuditEntry` rows. Go through `UnlockService` for unlocking.
  See [Closed-year lock and audit](docs/domain/closed-year-lock-and-audit.md).
- **Averages use the snapshotted `GradeBucket.Weight`**, never the live `BucketTemplate.Weight`.
  See [Grades, assessments and averages](docs/domain/grades-assessments-and-averages.md).
- **Business rules live in `NexGrades.Domain`** services. New rules go there, with tests, not into view models.
  See [where the rules live](docs/domain/README.md).
- **No pupil data in logs or telemetry.** Logs, metrics and traces never carry names, grades, notes, record ids or
  other record content, in a message, tag or metric dimension. Tag keys come only from `NexGradesTelemetry`. See
  `docs/decisions/0002-telemetry-and-privacy.md`, `docs/decisions/0003-application-logging.md` and
  [startup, logging and data paths](docs/domain/startup-logging-and-data-paths.md).

### Rules by area

**School year** ([page](docs/domain/school-year-lifecycle.md)):

- At most one year is Active.
- `SchoolYearService.CreateNextYearAsync` creates the Planned successor of the Active year; only one Planned
  successor may exist at a time.
- `YearCloseService.CloseAndActivateNextYearAsync` closes the Active year and activates the next one in a single
  transaction. It is the only way to switch years.
- Close readiness is reported as structured `CloseReadinessIssue` values, never as English sentences; the UI words
  them.

**Closed-year lock and audit** ([page](docs/domain/closed-year-lock-and-audit.md)):

- `UnlockService` unlocks, re-locks and reads the lock state.
- `UnlockedUntil` and `UnlockReason` are stored on the SchoolYear row, so audit entries keep the reason after an app
  restart; re-locking clears both.
- An `AuditEntry` is written for every change to a closed-but-unlocked year, carrying that year's `UnlockReason`.

**Classes and rollover** ([page](docs/domain/classes-cohorts-and-rollover.md)):

- A ClassGroup is the persistent cohort and carries no grades.
- A rename between years (5A → 6A) is a new ClassInstance row, never an update of a historic one.
- Renaming an Active instance changes `Name` in place; a rolled-forward or archived one is refused.
- A new or changed class name is at most 20 characters (`ClassService.MaxNameLength`). A longer name that
  is already stored, such as one rolled forward unchanged, stays valid and can be saved unchanged.
- `RolloverService.GetClassRolloverStatesAsync` lets a partly finished rollover resume from database state alone.

**Students and enrollment** ([page](docs/domain/students-and-enrollment.md)):

- A student has at most one enrollment per school year; this is enforced in the Domain (`EnrollmentInvariants`),
  not the schema.

**Subjects and buckets** ([page](docs/domain/subjects-buckets-and-participation.md)):

- A GradeBucket is a snapshot copied from the BucketTemplates when a SubjectInstance is created, with deliberately
  no FK back to the template, so editing a template never changes a past year's averages.
- SubjectParticipation rows exist only for deviations from the default "enrolled ⇒ participates" rule.

**Grades and assessments** ([page](docs/domain/grades-assessments-and-averages.md)):

- An assessment's bucket never changes; a grade with an assessment is always in the assessment's bucket.
- Grades and assessments are "removed" by voiding (`VoidedOn`), which can be undone.
- A grade counts only while neither it nor its assessment is voided: voided grades and grades of voided
  assessments are excluded from averages, the class overview export and close readiness.
- Grades are managed through `GradeService` and assessments through `AssessmentService`.
- The student page's Grades tab reads through `GradeSheetService.GetStudentGradesAsync` and corrects through
  `GradeService`, so the same rules apply as on the grade sheet (an exempt subject lists its grades without
  averages or a final grade).

**Grading scale and final grade** ([page](docs/domain/grading-scale-and-final-grade.md)):

- Always use `decimal` for grades, weights and averages — never `double`/`float`.
- An empty bucket or subject has **no** average (`null`), not zero (`AverageCalculator`).
- Keep scale-specific knowledge inside `GradingScale` (the German 1–6 scale, lower is better) so another scale can
  be added later without touching callers.
- The proposed final grade (`GradingScale.ProposeFinalGrade`) is derived, never stored.
- `FinalGrade` stores only the teacher's `OverrideValue`, at most one row per subject instance and student, created
  on the first override. Clearing sets `OverrideValue` to null instead of deleting the row.
- Final grade overrides are managed through `GradeService.SetFinalGradeOverrideAsync` and
  `ClearFinalGradeOverrideAsync`.
- An exempt student has no proposal and no override on the grade sheet, even if a row exists from before the
  exemption.

**Notes** ([page](docs/domain/notes.md)):

- A note (`NoteService`) needs an enrollment of the student in the note's school year and records its class.
- It is "removed" by archiving (`ArchivedOn`), which can be undone, and is locked like everything else in a closed
  year.

**Exports** ([page](docs/domain/exports.md)):

- The class overview export (`SubjectOverviewExportService`) lists no exempt students.
- The notes export (`NoteExportService`) filters notes on `CreatedOn` and leaves archived ones out unless asked for.

**Startup, logging and data paths** ([page](docs/domain/startup-logging-and-data-paths.md)):

- Pending migrations are applied at startup by `DatabaseMigrationService`, which `StartupDatabaseMigrator` calls; the
  migrator takes the backup described below first when one is needed. Then `SchoolYearService.EnsureActiveYearAsync`
  bootstraps an Active school year if none exists. `App.xaml.cs` calls the migrator and then `EnsureActiveYearAsync`,
  in that order.
- `Program.Main` runs `VelopackApp.Build().Run()` before `App` is created; it handles the installer's hook arguments
  and returns on a normal start.
- A startup failure at any step (host creation, configuration, database path, pre-migration backup, migration, host
  start) is logged at Critical, shown once in a message box, and ends the process with exit code 1
  (`StartupFailureHandler`, called from `App.OnStartup`).
- Log files go to `IAppDataPaths.LogDirectory` in every environment, and the database override does not move them.

**Settings, backups and updates** ([page](docs/domain/updates-backups-and-settings.md), decision
[0004](docs/decisions/0004-application-updates.md)):

- Preferences kept outside the database live in `settings.json` in `%LOCALAPPDATA%\NexGrades\`
  (`IAppDataPaths.SettingsFilePath`), read and written by `JsonUserSettingsStore` as `UserSettings`; a `sqlite`
  override does not move it. `LastRunVersion` names the next backup.
- `AppThemeService` applies the saved theme before the main window shows, and `Settings:FilePath` (environment
  variable `Settings__FilePath`) moves only `settings.json`, for screenshots and tests.
- Before a migration of an existing database, `DatabaseBackupService` makes a backup that must pass
  `PRAGMA quick_check`; a failed backup stops startup before anything is migrated. Nothing restores a backup
  automatically. Backups hold pupil data, like the database (see `docs/decisions/0001-encryption-at-rest.md`).
- `UpdateCoordinator` stays inactive, and makes no network request, unless the app runs from a Velopack install, so
  development, the AppHost and the tests never check for updates. `Updates:SourceOverride` is empty in shipped
  builds.
- `NexGradesPackId` in `NexGrades.App.csproj` is the Velopack pack id and must differ from the data folder name
  `NexGrades`. Changing it would leave installed apps unable to find their updates.

## Coding conventions

Match the surrounding code. The root `.editorconfig` is the source of truth for C# style (file-scoped
namespaces, `var`, `System` usings first, `_camelCase` private fields, braces, Allman layout) and for
indentation in XAML, project and JSON files. Run `dotnet format NexGrades.slnx` before committing. In
particular:

- **Static analysis:** `src/NexGrades/Directory.Build.props` turns on the .NET analyzers
  (`AnalysisMode=Recommended`) and adds Roslynator, Meziantou.Analyzer and the Microsoft.VisualStudio.Threading
  analyzers to every project. Severities are tuned in `.editorconfig`, each deviation with its reason. Fix a
  finding rather than silencing it; if a rule really does not fit, lower it in `.editorconfig` with a comment,
  or use `[SuppressMessage]` with a `Justification`. Async methods end in `Async` (`[RelayCommand]` strips the
  suffix from the generated command name).
  `EnforceCodeStyleInBuild` and `GenerateDocumentationFile` are on, so the `warning`-level IDE rules and missing
  XML doc comments on public members (CS1591) are reported at build; test projects are exempt from CS1591.
  `MA0048` means one type per file, named after the type. `IDE0130` means namespaces match folders. EF
  migrations are generated code and excluded from the analyzers in `.editorconfig`.
- **Language:** all code, identifiers, comments, commit messages and docs are in English.
- **Packages:** central package management. Add versions only to `src/NexGrades/Directory.Packages.props`;
  `PackageReference` items in `.csproj` files carry no `Version`. Do not remove or downgrade the
  `SQLitePCLRaw.bundle_e_sqlite3` pin (security advisory, see the comment there).
- **Package audit:** NuGet audit is on (`NuGetAuditMode=all`), and its warnings (NU1901-NU1904) are never
  suppressed. Fix a vulnerable transitive package by adding a `PackageVersion` for it, without a `PackageReference`,
  in `Directory.Packages.props`; `CentralPackageTransitivePinningEnabled` makes that version apply. `nuget.config` maps
  packages to sources, so a new feed needs a `packageSourceMapping` entry.
- **Domain services:** `sealed` classes with primary constructors, async methods taking a
  `CancellationToken cancellationToken = default`. Violated business rules throw
  `InvariantViolationException` with a message that names the offending ids/values. Bad input is reported the
  same way, including a grade token or value that is not on the scale. Never throw `ArgumentException` or
  `ArgumentOutOfRangeException` with a value in the message from Domain code: the file log redacts a Domain
  exception's message by its type namespace (`NexGrades.Domain*`), and a `System` exception is not redacted. Null
  checks (`ArgumentNullException.ThrowIfNull`) are fine, because their messages name only the parameter. Two
  `InvalidOperationException` throws in `ClosedYearAuditInterceptor` are the exception: their messages are constant
  or contain a type name only. See `docs/decisions/0003-application-logging.md`.
- **DbContext lifetime: one short-lived context per operation.** Pages and view models are resolved from
  the root DI provider and no scope exists around them, so nothing may depend on `AppDbContext` directly
  (it is registered as scoped). Inject `IDbContextFactory<AppDbContext>` instead, in view models and Domain
  services alike, and open the context with `await using` inside the method that needs it. A Domain
  service method owns its context, and its transaction if it needs one; an operation that must be atomic
  is one service method on one context, not several service calls. `NexGrades.App.Tests` resolves every
  registered App and Domain service from the root provider with scope validation to enforce this.
- **Time:** inject `IClock` and use `clock.Now`; never call `DateTime.Now`/`UtcNow` in Domain code.
- **Logging:** inject `ILogger<T>`. Serilog types stay in `NexGrades.App/Logging/` and the crash handlers in
  `App.xaml.cs`. Write log calls as
  `[LoggerMessage]` source-generated methods (CA1848 flags direct calls), with a fixed message template. Templates
  and arguments are logged as they are, so never put pupil data, record ids, school-year labels or exception text
  (such as `ex.Message`) into them. Pass the exception as the exception argument; the file log redacts Domain
  exception messages (see `docs/decisions/0003-application-logging.md`). A path, or any value that carries the
  Windows user name, is logged only under the `NexGrades.Local.Startup` category
  (`StartupDiagnostics.LocalOnlyCategory`), which the OpenTelemetry provider does not export. Add logging providers
  only in `AddAppLogging`; the one exception is the OpenTelemetry provider, which `AddServiceDefaults` adds (see 0002).
- **EF Core:** configure entities in `NexGrades.Data/EntityMapConfigs/<Entity>Configuration.cs`, not with
  attributes. Use `AsNoTracking()` for read-only queries. Never hand-edit generated migration files or the
  model snapshot — add a new migration instead.
- **View models:** derive from `NexGrades.App.Core.ViewModel`; use `[ObservableProperty]` fields
  (`_camelCase`) and `[RelayCommand]` methods. Open a short-lived context per operation with
  `await using var db = await dbContextFactory.CreateDbContextAsync(...)` (see the lifetime rule above).
  Report outcomes via `ISnackbarService`.
- **Year-scoped editing pages:** a page that edits one school year uses the shared lock in
  `NexGrades.App/Core/YearLock` and no lock logic of its own. Inject `YearLockViewModelFactory`, call `Create()`
  for `YearLock`, call `ShowAsync(schoolYearId)` when the year changes, bind write commands to `IsWritable` and
  re-check them on `WritabilityChanged`, and place `<yearLock:YearLockBar DataContext="{Binding ViewModel.YearLock}"/>`
  on the page. A list of years creates one view model per row with `Create(state)`. `SchoolYearLockMonitor` owns the
  state and the expiry timer, so do not add a timer to a page. A change of writability must not clear what the user
  has typed. A page that catches `ClosedYearException` from a write calls `YearLock.RefreshAsync()` afterwards,
  so the lock banner and writability reflect the new state. A user-started reload (a filter toggle or a refresh
  after a write) on a page with editable input checks for unsaved input and asks before dropping it; a
  writability change never clears typed input. `StudentPage` is the reference.
- **Feature folders:** each screen lives in `NexGrades.App/Features/<Area>/` as `<Name>Page.xaml` (+ code
  behind) and `<Name>ViewModel.cs`. Register every new page, view model and service in
  `NexGrades.App/Extensions/HostBuilderExtensions.cs`.
- **Page lifetimes and navigation:** navigation-root overview pages (Home, Classes, Students, Subjects, School year, Settings)
  and their view models are singletons. Add, edit and detail pages and their view models are transient. Always
  register a page and its view model with the same lifetime. To pass a parameter (such as an id) to another page,
  call `PageNavigator.NavigateWithHierarchy<TPage>(parameter)` (`NexGrades.App/Core/PageNavigator.cs`) from the
  source view model. The target view model reads it by overriding
  `protected override Task OnNavigatedToAsync(object? parameter)`. The parameter is `null` when the page is opened
  without one (an Add flow), so reset to the add state. Do not add per-page singleton "request" or mailbox objects.
  `PageLifetimeTests` in `NexGrades.App.Tests` enforces this. It finds pages by implementing
  `INavigableView<TViewModel>`, so a new page must implement it to be checked. It asserts that each page and its view
  model share a lifetime, that the navigation roots in its `_rootPages` set are singletons, and that every other page
  is transient. `HomePage` is in that set but is asserted by type, because it does not implement `INavigableView`.
  A new overview page must be added to `_rootPages`, or the test fails.
  A view model with unsaved input overrides `CanLeaveAsync` (default: allow) to ask before the user leaves by sidebar
  or back button; the shell and `PageNavigator` await it and cancel the navigation on `false`. A view model that
  leaves by itself after saving sets its own allow-leave flag first, so it does not prompt twice.
- **XAML:** use WPF-UI controls (`ui:Button`, `ui:TextBox`, `ui:TextBlock`, `ui:SymbolIcon`, …) and theme
  resources (`DynamicResource`) rather than hard-coded colors, so light and dark themes keep working.
- **Comments:** XML doc comments on public types and members, explaining intent and the *why*. Avoid
  comments that restate the code.

## UI text and localization

The UI is English first; multi-language support (German first) is planned. Write new UI code so it can be
localized later without a rewrite:

- All user-facing text is English.
- Never build a sentence by concatenating fragments; use a single format string with placeholders, since
  word order differs between languages.
- Format numbers, grade averages and dates for display with the current UI culture (German uses `2,3`,
  not `2.3`). Persisted and audit values use `CultureInfo.InvariantCulture`, as
  `ClosedYearAuditInterceptor` does.
- Grade tokens ("1+", "2-") are culture-neutral.
- Exception messages are diagnostic text and will not be translated; don't design UI that depends on
  showing them verbatim.
- Some existing pages show identifiers such as `AddClass` or `ClassName` instead of real text. Don't copy
  that pattern for new UI.

## Testing

- Tests live in `NexGrades.Tests` and cover Domain and Data. There are no UI tests; `NexGrades.App.Tests`
  only checks that the App's DI graph resolves, so it picks up new pages and services automatically. It also holds pure App-side unit tests, such as UI text formatting. `XamlLoadTests` creates each view on an STA thread to catch XAML that compiles but throws when loaded.
- Use `SqliteTestDatabase` (a real, migrated SQLite file — never the EF in-memory provider) and
  `FixedClock` for anything time-dependent. They are in `SqliteTestDatabase.cs` and `FixedClock.cs` (`NexGrades.Tests/`); `GradeSheetSeed.cs` builds grade sheet data. Construct Domain services with
  `db.CreateContextFactory()`, and assert through a fresh `db.CreateContext()`: the context that seeded the
  data still tracks the old values.
- Name tests `Method_Outcome_Condition`, e.g. `RecordGradeAsync_Throws_ForIllegalGradeValue`.
- Every new Domain rule or invariant gets tests for both the allowed and the rejected case.
- `dotnet test` must pass before a change is considered done.

## Git and pull requests

- Branch off `dev` as `feature/<issue-number>-<short-slug>`; open pull requests against `dev`.
- Versions are not set in the project files: MinVer derives them from `vX.Y.Z` git tags, and CI publishes a beta
  pre-release on every push to `dev` and a stable release on every push to `main`. See `docs/releasing.md`.
- Commit subjects are short and imperative ("Add edit-class flow on the year-scoped model").
- One logical change per PR; reference the issue it closes.
- To take in newer `dev`, merge `origin/dev` into the feature branch; don't rebase or force-push a pushed
  branch.
- Never commit database files, user secrets or local settings (`.claude/settings.local.json`).
- **Line endings:** `.gitattributes` sets `* text=auto`, so Git normalizes line endings on commit. Keep the
  existing line endings of any file you edit.

## Agent roles and workflows

Role definitions and step-by-step workflows are plain Markdown, so any tool can read them even though they
live in Claude Code's folders:

- `.claude/agents/`: specialist roles: `architect`, `csharp-coder`, `ui-designer`, `tester`, `reviewer`,
  `docs-writer`; plus `issue-orchestrator`, a coordinator that runs one issue end to end through those
  roles, never edits files and never merges. The lead starts one orchestrator per issue, each in its own
  worktree under `.claude/worktrees/issue-<n>`, so several can run in parallel; the lead merges.
- `.claude/skills/`: workflows:
  - `implement-issue`: the lead's front door: brief an issue-orchestrator, then verify-and-merge
  - `verify-and-merge`: the lead's merge gate checklist for a MERGE-READY pull request, then the squash-merge
  - `smoke-start`: the smoke start procedure (step 7 of the issue-orchestrator) for a pull request that touches `NexGrades.App`
  - `lead-handover`: the rules for the lead's local `HANDOVER.md`, which is never committed
  - `grill-me`: interviews the user to settle open decisions before work starts
  - `wpf-ui-design`: UI design rules for this app
  - `new-feature-page`: adds a Page + ViewModel + DI registration and navigation
  - `ef-migration`: adds and verifies an EF Core migration
