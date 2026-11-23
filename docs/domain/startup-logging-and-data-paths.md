# Startup, logging and data paths

This page covers where the database and the log files live, what the App does at startup, what happens when
startup fails, and how telemetry and privacy fit in.

## Database location

- In `Development`, the App reads its connection string from `ConnectionStrings:sqlite`. The Aspire AppHost
  supplies it. Running `NexGrades.App` on its own in Development needs that value from user secrets. Without
  it the App fails to start rather than fall back to the real per-user database, so a development build never
  touches the teacher's data.
- Outside Development the database defaults to `%LOCALAPPDATA%\NexGrades\app.db`. `IAppDataPaths` provides
  the paths.
- A `sqlite` key in configuration still overrides that location outside Development.
- On startup a legacy `app.db` next to the executable or in the working directory is moved to the per-user
  location once, by `LegacyDatabaseRelocator`. This happens only when there is no override, after
  `IAppDataPaths.EnsureCreated()` and before migrations. It never overwrites an existing target, and it
  accepts only an SQLite file that mentions the EF migrations history table.

## Startup sequence

`App.xaml.cs` (`OnStartup`) runs every step inside one `try`:

1. Create the host. File logging and the crash handlers come first (`AddAppLogging`), then the OpenTelemetry
   providers (`AddServiceDefaults`), then the services (`AddShell`, `AddDomain`, `AddFeatures`) and the database
   registration (`AddDatabase`).
2. Write the startup lines to the log, then create the telemetry listeners. A failure to set telemetry up is
   logged and the App runs without it; it is not a startup failure.
3. Set the language. `AppLanguageService.InitializeAsync` reads the saved language and `UiCultureApplier.Apply` makes
   its cultures the process cultures. Neither throws, and both run before anything formats a date or reads a text.
   See [UI text and localization](ui-text-and-localization.md#startup-order).
4. Relocate a legacy database, when there is no database override.
5. Apply pending migrations. `StartupDatabaseMigrator` calls `DatabaseMigrationService`, and takes the backup
   first when one is needed (see [Updates, backups and settings](updates-backups-and-settings.md)).
6. Call `SchoolYearService.EnsureActiveYearAsync`, which bootstraps an `Active` school year if none exists.
   See [School year lifecycle](school-year-lifecycle.md).
7. Apply the saved theme, then start the host, which shows the window.

Migrations run before the bootstrap, so the year is created in a migrated database. Before `App` is created,
`Program.Main` runs `VelopackApp.Build().Run()`; it handles the installer's hook arguments and returns on a
normal start.

## Startup failures

A failure at any step (host creation, configuration, database path, pre-migration backup, migration, host
start) is logged at Critical, shown once in a message box, and ends the process with exit code 1. The `catch` in `OnStartup`
calls `App.ReportStartupFailure`, which hands the exception to `StartupFailureHandler`. The box never shows
the exception text. It names the log folder only when the file logger was created. The handler guards
everything that can throw, so the box and the exit always happen.

The box text comes from the resources, so it is in the language of the app: the language set in step 3 of the
startup sequence, or the Windows language for a failure that comes before it. The log entry stays in English. See
[UI text and localization](ui-text-and-localization.md).

## Log files

Log files go to `%LOCALAPPDATA%\NexGrades\logs\` (`IAppDataPaths.LogDirectory`) in every environment, and the
database override does not move them.

- One file per day, `nexgrades-yyyyMMdd.log`. It rolls to `nexgrades-yyyyMMdd_001.log` when it reaches
  `Logging:File:MaxFileSizeMegabytes` (3 by default).
- `Logging:File:RetainedFileCount` (14 by default) files are kept.
- An unparseable value in `Logging:File` falls back to its default. A value below 1 is raised to 1.
- `Logging:LogLevel` and `Logging:File` in `NexGrades.App/appsettings.json` configure this, and
  `appsettings.Development.json` overrides the levels only.
- The AppHost's own `appsettings*.json` configure the AppHost, not the App.
- If the log folder cannot be used, the App runs without a file log.

See [0003: Application logging](../decisions/0003-application-logging.md).

## Telemetry

To see logs, metrics and traces live, run the AppHost and open the Aspire dashboard at the URL it prints.
Running `NexGrades.App` on its own exports nothing unless `OTEL_EXPORTER_OTLP_ENDPOINT` is set. See
[0002: Telemetry and privacy](../decisions/0002-telemetry-and-privacy.md).

## Privacy

NexGrades holds pupils' names, grades and notes, which is personal data about minors. Logs, metrics and
traces are another way for that data to leave the database, so they never carry names, grades, notes, record
ids or other record content, in a message, tag or metric dimension. Tag keys come only from
`NexGradesTelemetry`.

The file log is local and keeps more than telemetry does, so it redacts the messages of Domain exceptions,
which name record ids and values. Paths, which carry the Windows user name, are logged only under the
`NexGrades.Local.Startup` category, which the OpenTelemetry provider does not export. The reasoning is in
[0002: Telemetry and privacy](../decisions/0002-telemetry-and-privacy.md) and
[0003: Application logging](../decisions/0003-application-logging.md).

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.App/Storage/IAppDataPaths.cs`
- `NexGrades.App/Storage/AppDataPaths.cs`
- `NexGrades.App/Storage/LegacyDatabaseRelocator.cs`
- `NexGrades.App/App.xaml.cs`
- `NexGrades.App/Extensions/HostBuilderExtensions.cs`
- `NexGrades.App/Infrastructure/StartupFailureHandler.cs`
- `NexGrades.App/Localization/AppLanguageService.cs`
- `NexGrades.App/Localization/UiCultureApplier.cs`
- `NexGrades.App/Logging/FileLogging.cs`
- `NexGrades.App/Logging/FileLoggingOptions.cs`
- `NexGrades.App/Shell/ApplicationHostService.cs`
- `NexGrades.Data/Services/DatabaseMigrationService.cs`
- `Aspire/NexGrades.ServiceDefaults/ServiceDefaultsExtensions.cs`

## Related

- [School year lifecycle](school-year-lifecycle.md)
- [Closed-year lock and audit](closed-year-lock-and-audit.md)
- [0001: Encryption at rest for the database](../decisions/0001-encryption-at-rest.md)
- [0002: Telemetry and privacy](../decisions/0002-telemetry-and-privacy.md)
- [0003: Application logging](../decisions/0003-application-logging.md)
