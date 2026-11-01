# 0003: Application logging

- Status: Accepted
- Date: 2026-11-01
- Issue: #79

## Context

Telemetry (0002) reaches the Aspire dashboard only when `OTEL_EXPORTER_OTLP_ENDPOINT` is set, and shipped builds
do not set it. Before this change nothing was written to disk, so a failure on a teacher's machine (startup, a
migration, an unhandled exception) could not be looked at afterwards. The app needs a local log file.

A log file is a second copy of information about pupils on the same device as the database. Its content follows
the telemetry rules (0002), with one exception that is decided below.

## Options considered

1. **No file log.** Nothing to keep, rotate or protect. Failures on shipped builds cannot be diagnosed.
2. **Serilog file sink behind `Microsoft.Extensions.Logging`.** Chosen. Code keeps using `ILogger<T>`; Serilog only
   writes the file. `Serilog.Sinks.File` provides daily and size rolling and file-count retention.
3. **Serilog configured from `appsettings.json` (`Serilog.Settings.Configuration`).** Rejected. It would add a
   second level configuration next to the `Logging:LogLevel` section that MEL already applies to every provider.
4. **A hand-written file provider.** Rejected. Rolling, retention and safe sharing between writers would all have
   to be written and tested here.

## Decision

- **Code logs only through `ILogger<T>`.** Serilog is an implementation detail of
  `LoggingBuilderExtensions.AddAppLogging`, which calls `AddSerilog`. The packages are `Serilog.Extensions.Logging`
  and `Serilog.Sinks.File`, with versions in `Directory.Packages.props`. `Serilog.Settings.Configuration` is not used.
- **Levels come from `Logging:LogLevel`** in `NexGrades.App/appsettings.json`. `appsettings.Development.json`
  overrides the levels in Development. MEL applies these filters before any provider, so they apply to the file
  and to OpenTelemetry alike (see Consequences).
- **File limits come from `Logging:File`**: `RetainedFileCount` (14) and `MaxFileSizeMegabytes` (3), read by
  `FileLoggingOptions`. Values below 1 are raised to 1, so a bad setting cannot disable rolling. The folder is
  capped at about 42 MB (14 × 3 MB). Retention counts files, not days, and size rollovers count as files.
- **Location:** `IAppDataPaths.LogDirectory`, which is `%LOCALAPPDATA%\NexGrades\logs\`. The folder is used in every
  environment, including Development, and is not moved by the `sqlite` override. `FileLogging.TryCreate` creates it;
  `IAppDataPaths.EnsureCreated` does not.
- **File names:** `nexgrades-yyyyMMdd.log`, one per day. When a file reaches the size limit, the sink continues in
  `nexgrades-yyyyMMdd_001.log`, then `_002`, and so on.
- **Line format** (`LogFileFormatter`): `2026-10-31 14:03:07.123 +01:00 [INF] Category Message`, one event per line,
  with the level codes `VRB`, `DBG`, `INF`, `WRN`, `ERR` and `FTL`. Timestamps are written with the invariant
  culture. The file is UTF-8 without a byte order mark. If the event has an exception, its text follows on the
  next lines.
- **Setup order.** `AddAppLogging` calls `ClearProviders` first. That removes the default Console and EventLog
  providers, which would write exception messages without the redaction below. It must run before
  `AddServiceDefaults`, which adds the OpenTelemetry provider; clearing after it would remove that provider. The
  Debug provider is added in Development only. No other code adds a logging provider.
- **Exception messages in the file (decision).** `ExceptionDescriber` replaces the message of an exception whose
  type is in a `NexGrades.Domain*` namespace with `[message redacted]`. An `AggregateException` is redacted when
  any of its inner exceptions, at any depth, is. The type name, the stack trace and the inner exception chain are
  kept. Messages of framework and third-party exceptions are kept as they are.
  Why: Domain messages name record ids and values, because AGENTS requires `InvariantViolationException` messages to
  name the offending ids and values. The Domain types that exist today are `InvariantViolationException` and its
  subclass `ClosedYearException`. Framework messages rarely contain record content, and they are needed for diagnosis.
- **Residual risk, accepted.** Framework messages and file paths can contain the Windows user name. The log and
  database paths are written at startup, so every log file contains the user name. Some framework messages may
  include SQL text or values. This is accepted because the file stays on the device and is never exported.
- **OpenTelemetry redaction is unchanged.** `ExceptionRedactionLogProcessor` and the other settings from 0002 are
  not changed. The processor runs inside the OpenTelemetry provider, so every record that provider receives passes
  through it before export. The file has its own formatter and does its own redaction, so neither rule depends on
  the other.
- **Crash handlers** (`App.InstallCrashHandlers`, installed as soon as the file logger exists):
  - `DispatcherUnhandledException` is logged at Error. `Handled` stays false, so the crash behaviour is unchanged.
  - `AppDomain.UnhandledException` is logged at Critical. When the runtime is terminating, the log is flushed
    (`Log.CloseAndFlush`) before the process ends.
  - `TaskScheduler.UnobservedTaskException` is logged at Error and marked observed, so the task does not end the process.
  - A failure to create the host is logged at Critical, and the exception is rethrown.
  If the file logger could not be created, these handlers write nothing.
- **An unusable folder is tolerated.** If the folder cannot be created or the file cannot be opened,
  `FileLogging.TryCreate` returns `null` and the app runs without a file log. Only the exception type is written to
  the debugger output, because the message can contain paths.
- **Startup and shutdown lines.** Once the host is built, startup writes the NexGrades version, OS, .NET runtime and
  environment, then the database file (the data source only, never the full connection string) and the log folder.
  Shutdown writes `NexGrades is shutting down`, and after the host has stopped, `NexGrades stopped`.
- **Not part of this decision.**
  - The startup error dialog (the `MessageBox` in `App.OnStartup`) stays with #51. A startup failure is now logged
    at Critical before the dialog is shown.
  - "Open log folder" in Settings is deferred to a follow-up. There is no log viewer and no export.

## Consequences

- MEL applies `Logging:LogLevel` to every provider, so the Aspire dashboard receives the same levels as the file when
  OTLP export is enabled. With the shipped settings that is `NexGrades` at Information and above. In Development,
  `NexGrades` is at Debug, so Debug records reach the dashboard too.
- The local log files hold more than telemetry does: unredacted framework messages, file paths and the Windows user
  name. The log folder needs the same care as the database (0001). Device encryption protects it, and copies of the
  folder on unencrypted media are not protected.
- The folder holds at most 14 files (about 42 MB). The sink deletes the oldest files when the limit is exceeded,
  without writing a log line about it.
- Log calls must follow the rules in `AGENTS.md`: fixed templates, no record content in templates or arguments,
  exceptions passed as the exception argument.
- The telemetry rule from 0002, that exception messages are not recorded, applies to exported telemetry only.

## Revisit when

- A log viewer, "Open log folder" or log export is built. Export moves data off the device and needs its own consent
  decision (see 0002).
- A new exception type outside the `NexGrades.Domain*` namespaces carries record content. Add a rule for it in
  `ExceptionDescriber`, and record the change here.
- Framework messages turn out to carry record content in practice. Then redact all messages in the file, not only
  the Domain ones.
