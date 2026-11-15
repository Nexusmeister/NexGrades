# NexGrades domain

This folder explains what the NexGrades model is and why it has its shape. `AGENTS.md` keeps only the short
rules that follow from it. Read the page for an area before you change code, data or behaviour there.

The XML doc comments on the entities in `NexGrades.Data/Entities/` stay the most detailed source for each
column.

## Pages

| Page | What it covers | Key types |
|---|---|---|
| [School year lifecycle](school-year-lifecycle.md) | Planned, Active and Closed; creating the next year; closing a year; close readiness | `SchoolYear`, `SchoolYearService`, `YearCloseService`, `CloseReadinessIssue` |
| [Classes, cohorts and rollover](classes-cohorts-and-rollover.md) | Cohorts and yearly classes, renaming, rolling a class into the next year, the teacher | `ClassGroup`, `ClassInstance`, `ClassService`, `RolloverService`, `Teacher` |
| [Students and enrollment](students-and-enrollment.md) | Students and their class membership | `Student`, `Enrollment`, `EnrollmentService`, `EnrollmentInvariants` |
| [Subjects, buckets and participation](subjects-buckets-and-participation.md) | The subject catalogue, bucket templates and snapshots, who takes part in a subject | `Subject`, `BucketTemplate`, `SubjectInstance`, `GradeBucket`, `SubjectParticipation` |
| [Grades, assessments and averages](grades-assessments-and-averages.md) | Grades, assessments, voiding, averages, the student Grades tab | `Grade`, `Assessment`, `GradeService`, `AssessmentService`, `AverageCalculator`, `GradeSheetService` |
| [Grading scale and final grade](grading-scale-and-final-grade.md) | The 1 to 6 scale, tokens, the proposed final grade and the teacher's override | `GradingScale`, `FinalGrade` |
| [Closed-year lock and audit](closed-year-lock-and-audit.md) | Read-only closed years, unlocking, how a row's year is found, audit entries, no deletes | `ClosedYearAuditInterceptor`, `SchoolYearResolver`, `UnlockService`, `AuditEntry` |
| [Notes](notes.md) | Free-text notes on a student | `Note`, `NoteService` |
| [Exports](exports.md) | The class overview export and the notes export | `SubjectOverviewExportService`, `NoteExportService` |
| [Startup, logging and data paths](startup-logging-and-data-paths.md) | Database location, startup order and failures, log files, telemetry, privacy | `IAppDataPaths`, `LegacyDatabaseRelocator`, `StartupFailureHandler`, `FileLogging` |
| [Updates, backups and settings](updates-backups-and-settings.md) | The preferences file, the backup before a migration, self-update, the Velopack pack id | `JsonUserSettingsStore`, `DatabaseBackupService`, `UpdateCoordinator` |

## Glossary

- **SchoolYear**: the root scope of the model (for example "2025/26"); Planned, Active or Closed. See [School year lifecycle](school-year-lifecycle.md).
- **ClassGroup**: the persistent cohort across years; carries no grades. See [Classes, cohorts and rollover](classes-cohorts-and-rollover.md).
- **ClassInstance**: one cohort in one school year (for example "5A"); where teaching happens. See [Classes, cohorts and rollover](classes-cohorts-and-rollover.md).
- **Student**: a person; not year-scoped. See [Students and enrollment](students-and-enrollment.md).
- **Enrollment**: a student's membership of a class instance, with `JoinedOn` and `LeftOn`. See [Students and enrollment](students-and-enrollment.md).
- **Subject**: a catalogue entry (for example "Mathematik"); not year-scoped. See [Subjects, buckets and participation](subjects-buckets-and-participation.md).
- **BucketTemplate**: a subject's default bucket layout, copied into each new subject instance. See [Subjects, buckets and participation](subjects-buckets-and-participation.md).
- **SubjectInstance**: a subject taught to one class instance in one year. See [Subjects, buckets and participation](subjects-buckets-and-participation.md).
- **GradeBucket**: a weighted group of grades in a subject instance, copied from the templates. See [Subjects, buckets and participation](subjects-buckets-and-participation.md).
- **Assessment**: one graded occasion in a bucket and one column of the grade sheet. See [Grades, assessments and averages](grades-assessments-and-averages.md).
- **Grade**: one numeric grade in a bucket for a student. See [Grades, assessments and averages](grades-assessments-and-averages.md).
- **FinalGrade**: the teacher's override of a student's whole-grade final grade in a subject instance. See [Grading scale and final grade](grading-scale-and-final-grade.md).
- **SubjectParticipation**: a per-student deviation from the default "enrolled means participates" rule. See [Subjects, buckets and participation](subjects-buckets-and-participation.md).
- **Note**: a free-text note on a student within a school year. See [Notes](notes.md).
- **Teacher**: in practice one row, the app owner. See [Classes, cohorts and rollover](classes-cohorts-and-rollover.md).
- **AuditEntry**: one recorded change to a closed-but-unlocked school year. See [Closed-year lock and audit](closed-year-lock-and-audit.md).

## Where the rules live

Business rules live in `NexGrades.Domain` (mostly `Services/`, also `Grading/`, `Auditing/` and `Export/`), not in view models. Each service opens its own
short-lived context per method.

- `SchoolYearService`: the startup bootstrap of an Active year, the Planned successor.
- `YearCloseService`: close readiness, closing and activating the next year.
- `RolloverService`: rolling a class forward, archiving it, rollover state.
- `ClassService`: creating and renaming classes.
- `StudentService`: creating and renaming students, enrolling them on creation.
- `EnrollmentService`: enrolling a student, with the one-enrollment-per-year rule.
- `SubjectService`: adding subjects to the catalogue.
- `GradeService`: recording, changing, voiding and restoring grades; final grade overrides.
- `AssessmentService`: creating, editing, voiding and restoring assessments.
- `GradeSheetService`: the grade sheet of a subject instance and the grades of one student in one year.
- `AverageService`: loads the data for averages and hands it to `AverageCalculator`.
- `UnlockService`: unlocking and re-locking closed years, reading lock state.
- `NoteService`: creating, editing, archiving and listing notes.
- `NoteExportService` and `SubjectOverviewExportService` (in `NexGrades.Domain/Export/`): build the export tables.

## Decisions

- [0001: Encryption at rest for the database](../decisions/0001-encryption-at-rest.md)
- [0002: Telemetry and privacy](../decisions/0002-telemetry-and-privacy.md)
- [0003: Application logging](../decisions/0003-application-logging.md)
- [0004: Application updates](../decisions/0004-application-updates.md)
