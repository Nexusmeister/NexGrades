# Contributing to NexGrades

Pull requests are welcome. This file covers building, running and the project layout. The conventions for code and
docs are in [AGENTS.md](AGENTS.md). That file is the single source of truth, and AI coding tools read it too. Read it
before you change code.

## Prerequisites

- Windows. NexGrades is a WPF app and runs only on Windows.
- The .NET 10 SDK. CI installs `10.x.x`, the `DOTNET_VERSION` set in `.github/workflows/ci.yml`.
- The `dotnet-ef` global tool, only if you add an EF Core migration: `dotnet tool install --global dotnet-ef`.
- Docker, or another container runtime that Aspire supports, to run the AppHost. The AppHost starts SQLite Web
  (`WithSqliteWeb()`), which runs in a container. The Aspire SDK comes from NuGet with restore, so no separate
  Aspire install is needed.

## Build, test and run

Run these from `src/NexGrades/`:

```sh
dotnet restore
dotnet build NexGrades.App/NexGrades.App.csproj
dotnet build NexGrades.slnx -warnaserror                  # build every project; warnings fail the build, as in CI
dotnet test
dotnet format NexGrades.slnx                              # apply the .editorconfig style rules
dotnet format NexGrades.slnx --verify-no-changes          # fail if any file would change, as in CI
dotnet run --project Aspire/NexGrades.AppHost             # run the app for development
```

The build must produce no warnings. CI treats warnings as errors and also checks formatting. The full rules are in
[AGENTS.md](AGENTS.md#commands). `NexGrades.App.Tests` runs on Windows only.

## Running the app

- `dotnet run --project Aspire/NexGrades.AppHost` starts the App in Development, with SQLite Web and the Aspire
  dashboard. Open the dashboard at the URL the command prints to see logs, metrics and traces.
- Development never uses the real database in `%LOCALAPPDATA%`. The AppHost supplies `ConnectionStrings:sqlite`.
- Running `NexGrades.App` on its own in Development needs that connection string from user secrets. Point it at a
  scratch database, never at a real `app.db`. From `src/NexGrades/`:

  ```powershell
  dotnet user-secrets set "ConnectionStrings:sqlite" "Data Source=C:\path\to\scratch.db" --project NexGrades.App
  dotnet run --project NexGrades.App
  ```

- Without the AppHost, nothing is exported to telemetry unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set. See
  [0002](docs/decisions/0002-telemetry-and-privacy.md).

## Project layout

Paths are relative to `src/NexGrades/`. The dependency direction is App → Domain → Data. Details are in
[AGENTS.md](AGENTS.md#repository-layout).

| Project | Role |
|---|---|
| `NexGrades.App` | WPF shell, pages, view models, DI setup, file logging |
| `NexGrades.Domain` | Business rules: services, grading, closed-year auditing |
| `NexGrades.Data` | EF Core context, entities, entity configurations, migrations |
| `NexGrades.Common` | Small shared helpers and service interfaces |
| `NexGrades.Tests` | xUnit tests for Domain and Data |
| `NexGrades.App.Tests` | xUnit tests for the App's DI composition (Windows only) |
| `Aspire/NexGrades.AppHost` | Development-only orchestration (SQLite and SQLite Web) |
| `Aspire/NexGrades.ServiceDefaults` | OpenTelemetry setup, used by the App |

## Branching and pull requests

- Branch off `dev` as `feature/<issue-number>-<short-slug>`. Open pull requests against `dev`.
- Keep one logical change per pull request, and reference the issue it closes.
- Write short, imperative commit subjects, for example "Add edit-class flow on the year-scoped model".
- To take in newer `dev`, merge `origin/dev` into your branch. Do not rebase or force-push a branch that is already
  pushed.
- CI runs the tests, the build with `-warnaserror` and the format check on every push. A pull request needs them
  green.
- Never commit database files, user secrets or local settings such as `.claude/settings.local.json`.

## Releases and versions

Versions come from git tags (`vX.Y.Z`) through MinVer. They are not set in the project files. Each push to `dev` that
passes the tests and build publishes a beta pre-release. Each merge to `main` publishes the next patch version as the
latest stable release. After a release on `main`, merge `main` back into `dev`. The full rules, including how to
choose a minor or major version, are in [docs/releasing.md](docs/releasing.md).

## Decisions

Architecture decisions are recorded as numbered records in [docs/decisions](docs/decisions/):

- [0001: Encryption at rest for the database](docs/decisions/0001-encryption-at-rest.md)
- [0002: Telemetry and privacy](docs/decisions/0002-telemetry-and-privacy.md)
- [0003: Application logging](docs/decisions/0003-application-logging.md)

Record a new decision as `docs/decisions/NNNN-<slug>.md`, using the next free number.

## Privacy rule

Pupil data must never appear in logs, telemetry, tests, issues or pull requests. That covers names, grades, notes,
record ids and school-year labels. Use made-up data in tests, issues, pull requests and screenshots. The screenshots
in `docs/images` use fictional demo data only. The rules are in [0002](docs/decisions/0002-telemetry-and-privacy.md)
and [0003](docs/decisions/0003-application-logging.md). The local log file also keeps some framework error text, so
treat it as sensitive too.
