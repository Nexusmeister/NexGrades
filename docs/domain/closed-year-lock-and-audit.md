# Closed-year lock and audit

A `Closed` school year is read-only. This page covers how the lock works, how a changed row is traced to its
year, what happens on a write or a delete, and what an audit entry records.

## Lock state

A `Closed` year is read-only unless it is temporarily unlocked. `SchoolYear.UnlockedUntil` holds the expiry
and `SchoolYear.UnlockReason` the reason. A year counts as unlocked only while `UnlockedUntil` is strictly
later than now. `SchoolYearLock.IsWritable` is the single definition of "can this year be written to right
now". The interceptor enforces it, and services and the UI use it to tell the user in advance, so the two
cannot disagree.

Both fields are stored on the `SchoolYear` row, not in memory, so audit entries keep the reason after an app
restart. Re-locking clears both. The next unlock replaces them.

`UnlockService` owns the lock state:

- `UnlockAsync` unlocks a `Closed` year until a future time and records a reason. The reason is trimmed and
  must not be empty or longer than `UnlockService.MaxReasonLength` (500).
- `RelockAsync` clears both fields immediately.
- `GetLockStateAsync` reads the lock fields of one year, and `GetClosedYearsAsync` lists every `Closed` year,
  newest first.

Unlocking and re-locking are not audited themselves, because a `SchoolYear` write is outside the audit
mechanism.

## How a row's school year is found

The school year is derived by traversal. `SchoolYearResolver` walks these paths:

- `Grade` to `GradeBucket` to `SubjectInstance` to `ClassInstance` to `SchoolYear`
- `Assessment` to `GradeBucket` to `SubjectInstance` to `ClassInstance` to `SchoolYear`
- `FinalGrade` to `SubjectInstance` to `ClassInstance` to `SchoolYear`

It also resolves `GradeBucket`, `SubjectInstance`, `SubjectParticipation` and `Enrollment` through their
parents. `Note` and `ClassInstance` carry a `SchoolYearId` and resolve directly. `Student`, `Subject`,
`BucketTemplate`, `Teacher`, `ClassGroup`, `SchoolYear` and `AuditEntry` have no year association, so they are
outside the lock and never audited by it.

The model has no denormalized `SchoolYearId` on the other entities: there is no year column to get out of
sync with the real parent chain. Only `SchoolYear`, `ClassInstance` and `Note` carry one. `ClassGroup` has
`FoundedInSchoolYearId`, which is the cohort's founding year, not a scoping column.

## Writes to a closed year

`ClosedYearAuditInterceptor` is an EF Core `SaveChangesInterceptor`. A write to any entity reachable from a
`Closed` year throws `ClosedYearException` unless the year is unlocked, and every allowed write produces
`AuditEntry` rows. Its design has these parts:

- **Two phases.** Before the save, the interceptor rejects a write to a locked year by throwing, so nothing
  is persisted. For an allowed write it only collects the changes, because a new entity's `Id` is not yet
  known. After the save, it builds the `AuditEntry` rows from the real ids and writes them in a second small
  save on the same context.
- **Atomicity.** The primary write and its audit rows commit or roll back together. If the caller has no
  transaction open, the interceptor opens one and owns it. If the caller already has one (as
  `EnrollmentService` and `RolloverService` do), the interceptor takes part in it.
- A year that is not `Closed`, or an entity with no year, has no restriction and no audit trail.

## Deletes

Nothing is ever deleted. Entities change status instead (voiding, archiving, status values). The interceptor
rejects every `Deleted` entry for every entity type, whatever the year. For an entity of a `Closed` year the
error is a `ClosedYearException`. For any other entity it is an `InvariantViolationException`.

## AuditEntry

An `AuditEntry` is written for every change to a closed-but-unlocked year, and carries that year's
`UnlockReason` in `Reason`. It identifies the changed row by `EntityType` and `EntityId`, not by a foreign
key, so one table can audit any entity. A new row produces one entry (`Field` is `<created>`, with a summary
of the values in `NewValue`). A changed row produces one entry per changed property, with `OldValue` and
`NewValue` as text. Values are formatted with `CultureInfo.InvariantCulture`, so the audit log reads the same
on any machine.

## In the UI

A page that edits one school year uses the shared year lock. See the "Year-scoped editing pages" convention
in [AGENTS.md](../../AGENTS.md).

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.Domain/Auditing/ClosedYearAuditInterceptor.cs`
- `NexGrades.Domain/Auditing/SchoolYearResolver.cs`
- `NexGrades.Domain/Auditing/SchoolYearLock.cs`
- `NexGrades.Domain/Services/UnlockService.cs`
- `NexGrades.Domain/Exceptions/ClosedYearException.cs`
- `NexGrades.Data/Entities/AuditEntry.cs`

## Related

- [School year lifecycle](school-year-lifecycle.md)
- [Notes](notes.md)
- [Startup, logging and data paths](startup-logging-and-data-paths.md)
