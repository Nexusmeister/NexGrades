# 0002: Telemetry and privacy

- Status: Accepted
- Date: 2026-10-31
- Issue: #74

## Context

NexGrades holds pupils' names, grades and notes. That is personal data about minors (see 0001). While
developing, the developer wants logs, metrics and traces in the Aspire dashboard. Telemetry is a second way
for data to leave the process, so it is designed with the same care as the database file.

## Options considered

1. **No telemetry.** Nothing can leak and nothing needs maintaining. The developer has no view of startup,
   migrations or Domain operations.
2. **Export always on.** Telemetry is sent whenever the app runs, to a configured collector. Simple, but every
   installed copy would export by default, which is hard to justify for data about minors.
3. **Opt-in via the `OTEL_EXPORTER_OTLP_ENDPOINT` environment variable.** The OTLP exporter is added only when
   the standard OpenTelemetry endpoint variable is set. Chosen.

## Decision

- The OTLP exporter is added only when `OTEL_EXPORTER_OTLP_ENDPOINT` holds a non-blank value
  (`ServiceDefaultsExtensions.IsOtlpExportEnabled`). The providers still run in process, but nothing is exported
  and there is no telemetry network traffic.
- Aspire injects the variable into the App when the App is launched from the AppHost. The AppHost code does not
  set it itself. Shipped builds do not have it set.
- All app telemetry goes through `NexGradesTelemetry`: one `ActivitySource` and one `Meter`, both named
  `NexGrades`. Tag keys are a closed set, `NexGradesTelemetry.AllowedTagKeys`: `nexgrades.operation`,
  `nexgrades.outcome`, `nexgrades.page` and `error.type`.
- No record content appears in telemetry. Span names and tag values are fixed operation names, outcomes, page
  type names and exception type names. Exception messages are not recorded; only the exception type name is.
  Record ids (student, enrollment, note and so on) and school-year labels are not allowed either.
- Exception messages and stack traces are removed from every exported log record, including those logged by the
  framework (EF Core, hosting): `ExceptionRedactionLogProcessor` replaces the exception with an
  `exception.type` attribute holding the type name. It is registered in the same logging pipeline before
  `UseOtlpExporter`, so it runs before the record is copied for export.
- Formatted log messages and log scopes are not exported (`IncludeFormattedMessage = false`,
  `IncludeScopes = false`). Structured log arguments still travel as attributes. EF Core's
  `Microsoft.EntityFrameworkCore.Database.Command` category is raised to `Warning` for the OpenTelemetry
  provider, so executed SQL text is not exported.
- Instrumentation beyond `NexGradesTelemetry` is limited to HttpClient (traces and metrics), runtime metrics and
  EF Core's own meter, `Microsoft.EntityFrameworkCore`. No EF Core trace source is added. Those libraries define
  their own tags, which are outside the allow-list.
- Rule for new code: never put pupil data in a log message, tag or metric dimension.

## Also decided

- The ServiceDefaults template's HTTP resilience is left out. Nothing uses `HttpClient` yet, and its default
  10 second attempt timeout would not suit the updater's downloads (#21), which picks its own policy.
- Service discovery is left out. The app calls fixed URLs, and the Aspire resources it uses arrive as
  connection strings.
- Health checks and `MapDefaultEndpoints` are left out. There is no web server, and nothing probes the app.
- The ASP.NET Core shared framework is not a dependency. Neither ServiceDefaults nor App references
  `Microsoft.AspNetCore.App`.

## Consequences

- A user who sets `OTEL_EXPORTER_OTLP_ENDPOINT` themselves exports non-sensitive telemetry to wherever it
  points. The app does not check the destination.
- Future instrumentation must use `NexGradesTelemetry` and its allow-list. New sources or meters need a review
  against this record.
- The allow-list and the no-pupil-data rule are conventions checked in review. No code enforces them: the tag
  set is not validated at run time. The log pipeline drops exception messages, but not structured log arguments
  or the message template: never put record content or ids into either.
- The local file log (0003) is a separate sink with its own rules. It keeps framework exception messages, which
  this record does not allow in telemetry. The telemetry rules here are unchanged.
- The startup span (`App.Startup`) and the migration span (`DatabaseMigrationService.Run`) are captured only
  because `App.xaml.cs` calls `InitializeTelemetry()` before the migration. Moving that call later drops them.

## Revisit when

- Logging is added (#51). Partly addressed by 0003 (#79), which adds the local file log. The startup error dialog
  stays with #51. Log calls must follow the rules above. Exceptions in log calls are reduced to their type name for
  export; the local file keeps framework exception messages and redacts Domain ones (0003).
- A shipped build needs telemetry, such as crash or usage reports. That needs its own consent decision. The
  developer switch is not one.
- Telemetry is exported by default to a service outside the teacher's device.
