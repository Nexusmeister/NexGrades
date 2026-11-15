# NexGrades

A Windows desktop grade book for a teacher: school years, classes, students, subjects, weighted grade buckets and
averages. It is for one user and keeps everything in a local SQLite database; there is no server.

*Screenshot: placeholder, to be added.*

## Features

- School years (Planned, Active, Closed). Closing a year and activating the next one is a single step, and a closed
  year is read-only until you unlock it.
- Classes, students and subjects per school year, with weighted grade buckets and averages.
- A grade sheet with assessments. Final grades are proposed from the subject average and can be overridden. The student page shows each subject's final grade read-only.
- Notes on students, archived instead of deleted.
- Excel exports: the overview of one subject on the class page ("Export to Excel"), and a student's notes for a
  date range on the student page ("Export notes…", Notes tab). The notes export has its own dialog for the range
  and for including archived notes.

## Install

Download a release from the [GitHub Releases page](https://github.com/Nexusmeister/NexGrades/releases) and use one of:

- `NexGrades-<version>-win-x64-Setup.exe`: installs NexGrades for your Windows user account. No administrator rights
  are needed.
- `NexGrades-<version>-win-x64-Portable.zip`: unzip it to a folder of your own and run `NexGrades.App.exe` from there.

Both update themselves. The other files on a release are the update feed; you don't need them. Uninstalling keeps
your data in `%LOCALAPPDATA%\NexGrades`.

If you used an earlier plain zip, install the Setup.exe or the portable zip once by hand. The plain zip does not
update itself.

NexGrades is not code-signed yet, so Windows SmartScreen may warn the first time. Choose **More info**, then
**Run anyway**.

### Updates

NexGrades checks for updates about ten seconds after it starts, downloads a newer version in the background and
installs it when you close the app. Under **Settings > Updates** you can turn automatic checks off, receive beta
releases, or check now. Turning beta releases off never moves you to an older version.

## Build and run (developers)

Prerequisites:

- Windows
- The [.NET 10 SDK](https://dotnet.microsoft.com/download)
- A clone with its git tags. Version numbers come from the `vX.Y.Z` tags (see [docs/releasing.md](docs/releasing.md)).

From `src/NexGrades/`:

```sh
dotnet restore
dotnet build NexGrades.slnx
dotnet test
dotnet run --project Aspire/NexGrades.AppHost
```

The AppHost starts NexGrades with a SQLite database and prints the URL of the Aspire dashboard, where logs, metrics
and traces are shown. Running `NexGrades.App` on its own needs a connection string from user secrets; see
[AGENTS.md](AGENTS.md).

Conventions, dependency rules and the domain model are in [AGENTS.md](AGENTS.md).

## Project layout

| Path | Contents |
|---|---|
| `src/NexGrades/NexGrades.App` | WPF app: pages, view models, startup, logging, updates |
| `src/NexGrades/NexGrades.Domain` | Business rules: grading, services, closed-year auditing, backups |
| `src/NexGrades/NexGrades.Data` | EF Core model, entity configurations and migrations |
| `src/NexGrades/NexGrades.Common` | Shared helpers and service interfaces |
| `src/NexGrades/NexGrades.Tests`, `NexGrades.App.Tests` | xUnit tests |
| `src/NexGrades/Aspire/` | AppHost and ServiceDefaults for development |
| `.github/workflows/ci.yml`, `.github/scripts/` | Build, test and release pipeline |
| `docs/` | [Releasing](docs/releasing.md) and architecture decisions in `docs/decisions/` |

## Data and backups

NexGrades keeps its files in `%LOCALAPPDATA%\NexGrades`:

- `app.db`: the database
- `backups\`: copies of the database, taken before an update changes it
- `logs\`: daily log files
- `settings.json`: update preferences

Before a migration of an existing database, NexGrades copies the database into `backups\`. Each backup is named
`app-<date>-<time>-v<version>.db`, after the time it was taken and the version that last used the database. The five
newest are kept. If the backup fails, NexGrades does not start and the database is unchanged.

To restore a backup by hand:

1. Quit NexGrades.
2. Rename `%LOCALAPPDATA%\NexGrades\app.db` to keep it aside, for example `app.db.old`.
3. Delete any `app.db-journal` file next to it, if one exists.
4. Copy the backup you want from `%LOCALAPPDATA%\NexGrades\backups\` into `%LOCALAPPDATA%\NexGrades\` and rename it
   to `app.db`.
5. Start NexGrades.

Backups contain the same data as the database, so the rules in *Data protection* apply to them too.

## Data protection

NexGrades stores grades and notes about students in a local SQLite database. The app does not encrypt
this file itself. Before you use it with real student data:

- Turn on device encryption for the drive that holds the database (BitLocker or Windows device
  encryption: Settings > Privacy & security > Device encryption, or BitLocker in Control Panel).
- Do not copy the database file or its backups to unencrypted media such as plain USB sticks or cloud folders.
- Check your state's and school's rules for student data on private devices. Many require the
  principal's approval.

Background and when this will be revisited: [docs/decisions/0001-encryption-at-rest.md](docs/decisions/0001-encryption-at-rest.md).
