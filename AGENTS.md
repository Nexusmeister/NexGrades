# AGENTS.md

Instructions for AI coding agents working in this repository. This file is the single source of truth
for project conventions; tool-specific files (e.g. `CLAUDE.md`) import or point to it rather than
duplicating it. Keep it current when a convention changes.

## What NexGrades is

A Windows desktop grade book for a teacher: school years, classes, students, subjects, weighted grade
buckets and averages. Single user, local SQLite database, no server.

## Stack

- .NET 10 (`net10.0`, App is `net10.0-windows`), C# with nullable reference types and implicit usings
- WPF with [WPF-UI](https://wpfui.lepo.co/) 4.x (Fluent controls, navigation, snackbar, theming)
- CommunityToolkit.Mvvm 8.x (`[ObservableProperty]`, `[RelayCommand]`)
- EF Core 10 on SQLite
- .NET Aspire AppHost for local development only
- xUnit for tests

## Repository layout

All code lives under `src/NexGrades/` (solution: `NexGrades.slnx`).

| Project | Role | References |
|---|---|---|
| `NexGrades.App` | WPF shell, pages, view models, DI wiring | Common, Data, Domain |
| `NexGrades.Domain` | Business rules: services, grading, closed-year auditing, `IClock` | Data |
| `NexGrades.Data` | EF Core `AppDbContext`, entities, entity configurations, migrations | — |
| `NexGrades.Common` | Small cross-cutting helpers and service interfaces | — |
| `NexGrades.Tests` | xUnit tests for Domain and Data | Data, Domain |
| `NexGrades.App.Tests` | xUnit tests for the App's DI composition (Windows only) | App |
| `Aspire/NexGrades.AppHost` | Dev-time orchestration (SQLite + SQLite Web) | App |
| `Aspire/NexGrades.ServiceDefaults` | Shared Aspire defaults | — |

Dependency direction is App → Domain → Data. Data must not reference Domain or App; Domain must not
reference App or WPF.

## Commands

Run from `src/NexGrades/`:

```sh
dotnet restore
dotnet build NexGrades.App/NexGrades.App.csproj
dotnet test
dotnet format NexGrades.slnx                                         # apply the .editorconfig style and whitespace rules
dotnet ef migrations add <MigrationName> --project NexGrades.Data --startup-project NexGrades.Data   # needs the dotnet-ef global tool
dotnet run --project Aspire/NexGrades.AppHost                        # run the app for development
```

- In `Development`, the App reads its connection string from `ConnectionStrings:sqlite`, which the Aspire
  AppHost supplies. Running `NexGrades.App` on its own in Development needs that value from user secrets.
  Outside Development the database defaults to `%LOCALAPPDATA%\NexGrades\app.db` (see `IAppDataPaths`;
  other per-user data such as logs belongs in the same folder). A `sqlite` key in configuration still
  overrides that. On startup a legacy `app.db` next to the executable or in the working directory is moved
  there once (`LegacyDatabaseRelocator`).
- Pending migrations are applied at startup by `DatabaseMigrationService`, which also bootstraps an Active
  school year if none exists.
- The App is a GUI and cannot be meaningfully exercised headless. Verify changes with a build of
  `NexGrades.App` plus `dotnet test`; say so explicitly when a change also needs a manual UI check.

## Domain model

Read the XML doc comments on the entities in `NexGrades.Data/Entities/` before changing the model —
they record *why* the model is shaped the way it is.

- **SchoolYear** — the root scope (e.g. "2025/26", August–July). Status `Planned` → `Active` → `Closed`.
  At most one year is Active. A Closed year is read-only unless temporarily unlocked (`UnlockedUntil`).
- **ClassGroup** — the persistent cohort across years. Carries no grades.
- **ClassInstance** — one cohort in one school year (e.g. "5A"); where teaching happens. A rename between
  years (5A → 6A) is a new row, never an update of a historic one.
- **Student** — a person; not year-scoped.
- **Enrollment** — Student ↔ ClassInstance membership with `JoinedOn`/`LeftOn`. A student has at most one
  enrollment per school year; this is enforced in the Domain (`EnrollmentInvariants`), not the schema.
- **Subject** — catalogue entry (e.g. "Mathematik"); not year-scoped. Owns **BucketTemplate**s.
- **SubjectInstance** — a subject taught to one ClassInstance in one year.
- **GradeBucket** — a *snapshot* copied from the BucketTemplates when a SubjectInstance is created, then
  independently editable. Deliberately no FK back to the template, so editing a template never changes a
  past year's averages.
- **Grade** — one numeric grade in a bucket for a student.
- **SubjectParticipation** — per-student override of the default "enrolled ⇒ participates" rule
  (elective, exemption, religion/ethics split). Rows exist only for deviations.
- **Note** — free-text note on a student within a school year, managed through `NoteService`. A note
  needs an enrollment of the student in that year and records its class. It is "removed" by archiving
  (`ArchivedOn`), which can be undone, and is locked like everything else in a closed year.
- **Teacher** — in practice one row (the app owner, `IsCurrentUser`).
- **AuditEntry** — written for every change to a closed-but-unlocked year.

### Invariants agents must not break

- **Nothing is ever deleted.** Entities change status instead. `ClosedYearAuditInterceptor` rejects every
  `Deleted` entry.
- **The school year is derived by traversal** (e.g. Grade → GradeBucket → SubjectInstance → ClassInstance →
  SchoolYear). Do not add denormalized `SchoolYearId` columns; only SchoolYear, ClassInstance and Note carry
  one.
- **Closed years are locked.** Writes to a Closed year throw `ClosedYearException` unless the year is
  unlocked, and every allowed write produces `AuditEntry` rows. Go through `UnlockService` for unlocking.
- **Averages use the snapshotted `GradeBucket.Weight`**, never the live `BucketTemplate.Weight`.
- **Business rules live in `NexGrades.Domain`** (services such as `GradeService`, `EnrollmentService`,
  `RolloverService`, `YearCloseService`). New rules go there, with tests, not into view models.

### Grading scale

The German 1–6 scale with tendencies, defined in `NexGrades.Domain/Grading/GradingScale.cs`:
16 legal values from 1+ (0.7) to 6 (6.0), stored as `decimal`, **lower is better**. The scale is fixed to
1–6 for now; keep scale-specific knowledge inside `GradingScale` so another scale can be added later
without touching callers.

- Always use `decimal` for grades, weights and averages — never `double`/`float`.
- An empty bucket or subject has **no** average (`null`), not zero (`AverageCalculator`).

## Coding conventions

Match the surrounding code. The root `.editorconfig` is the source of truth for C# style (file-scoped
namespaces, `var`, `System` usings first, `_camelCase` private fields, braces, Allman layout) and for
indentation in XAML, project and JSON files. Run `dotnet format NexGrades.slnx` before committing. In
particular:

- **Language:** all code, identifiers, comments, commit messages and docs are in English.
- **Packages:** central package management. Add versions only to `src/NexGrades/Directory.Packages.props`;
  `PackageReference` items in `.csproj` files carry no `Version`. Do not remove or downgrade the
  `SQLitePCLRaw.bundle_e_sqlite3` pin (security advisory, see the comment there).
- **Domain services:** `sealed` classes with primary constructors, async methods taking a
  `CancellationToken cancellationToken = default`. Violated business rules throw
  `InvariantViolationException` with a message that names the offending ids/values.
- **DbContext lifetime: one short-lived context per operation.** Pages and view models are resolved from
  the root DI provider and no scope exists around them, so nothing may depend on `AppDbContext` directly
  (it is registered as scoped). Inject `IDbContextFactory<AppDbContext>` instead, in view models and Domain
  services alike, and open the context with `await using` inside the method that needs it. A Domain
  service method owns its context, and its transaction if it needs one; an operation that must be atomic
  is one service method on one context, not several service calls. `NexGrades.App.Tests` resolves every
  registered App and Domain service from the root provider with scope validation to enforce this.
- **Time:** inject `IClock` and use `clock.Now`; never call `DateTime.Now`/`UtcNow` in Domain code.
- **EF Core:** configure entities in `NexGrades.Data/EntityMapConfigs/<Entity>Configuration.cs`, not with
  attributes. Use `AsNoTracking()` for read-only queries. Never hand-edit generated migration files or the
  model snapshot — add a new migration instead.
- **View models:** derive from `NexGrades.App.Core.ViewModel`; use `[ObservableProperty]` fields
  (`_camelCase`) and `[RelayCommand]` methods. Open a short-lived context per operation with
  `await using var db = await dbContextFactory.CreateDbContextAsync(...)` (see the lifetime rule above).
  Report outcomes via `ISnackbarService`.
- **Feature folders:** each screen lives in `NexGrades.App/Features/<Area>/` as `<Name>Page.xaml` (+ code
  behind) and `<Name>ViewModel.cs`. Register every new page, view model and service in
  `NexGrades.App/Extensions/HostBuilderExtensions.cs`.
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
  only checks that the App's DI graph resolves, so it picks up new pages and services automatically.
- Use `SqliteTestDatabase` (a real, migrated SQLite file — never the EF in-memory provider) and
  `FixedClock` for anything time-dependent. Both are in `TestSupport.cs`. Construct Domain services with
  `db.CreateContextFactory()`, and assert through a fresh `db.CreateContext()`: the context that seeded the
  data still tracks the old values.
- Name tests `Method_Outcome_Condition`, e.g. `RecordGradeAsync_Throws_ForIllegalGradeValue`.
- Every new Domain rule or invariant gets tests for both the allowed and the rejected case.
- `dotnet test` must pass before a change is considered done.

## Git and pull requests

- Branch off `dev` as `feature/<issue-number>-<short-slug>`; open pull requests against `dev`.
- Commit subjects are short and imperative ("Add edit-class flow on the year-scoped model").
- One logical change per PR; reference the issue it closes.
- Never commit database files, user secrets or local settings (`.claude/settings.local.json`).

## Agent roles and workflows

Role definitions and step-by-step workflows are plain Markdown, so any tool can read them even though they
live in Claude Code's folders:

- `.claude/agents/`: specialist roles: `architect`, `csharp-coder`, `ui-designer`, `tester`, `reviewer`,
  `docs-writer`.
- `.claude/skills/`: workflows:
  - `implement-issue`: end-to-end issue pipeline that uses the roles above
  - `grill-me`: interviews the user to settle open decisions before work starts
  - `wpf-ui-design`: UI design rules for this app
  - `new-feature-page`: adds a Page + ViewModel + DI registration and navigation
  - `ef-migration`: adds and verifies an EF Core migration
