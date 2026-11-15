# Classes, cohorts and rollover

A class exists at two levels: the cohort that lasts across years, and the class of one school year. This page
covers both, how a class is renamed, how it moves into the next year, and the teacher row.

## ClassGroup and ClassInstance

A `ClassGroup` is the persistent cohort. It survives renames across school years and carries no grades.
`ClassGroup.FoundedInSchoolYearId` records the year the cohort was first formed. It is a founding year, not a
scoping column, and never changes. A `ClassGroup` has a status (`Active` or `Ended`); `Ended` is set only by
an explicit teacher decision.

A `ClassInstance` is one cohort in one school year (for example "5A"). It is where teaching happens:
enrollments, subject instances and grades attach to it. `ClassInstance.SchoolYearId` is the class's year.
Its status is `Active`, `RolledForward` or `Archived`. The last two stay in the database as history.

`ClassService.CreateClassAsync` creates a new cohort and its first instance in the `Active` year, in one
transaction.

## Renaming

`ClassInstance.Name` is per instance. A rename between years (5A to 6A) is a new row, never an update of a
historic one.

`ClassService.RenameClassAsync` renames an `Active` instance in place. A rolled-forward or archived instance
keeps its historic name, so renaming it is refused. The cohort label of the `ClassGroup` is not changed.

A new or changed name is at most 20 characters (`ClassService.MaxNameLength`). This is not a schema limit. A
longer name that already exists, for example one rolled forward unchanged, stays valid and can be saved unchanged.

## Rollover

`RolloverService.RollClassInstanceForwardAsync` rolls an `Active` class into a `Planned` year that starts
after the class's own year, in one transaction:

- The old instance becomes `RolledForward`. Nothing else about it changes.
- A new instance of the same cohort and teacher is created in the target year, with the new name. The new name is at most 20 characters, unless it is the old name unchanged.
- Active enrollments are carried over, through the same one-enrollment-per-year check that
  `EnrollmentService` uses. See [Students and enrollment](students-and-enrollment.md).
- Each subject instance is copied. Its buckets are copied from the current `BucketTemplate` rows, not from
  the old snapshot. Participation overrides are copied for the students who carried over. See
  [Subjects, buckets and participation](subjects-buckets-and-participation.md).

A cohort can have only one class in the target year.

`RolloverService.ArchiveClassInstanceAsync` is the other disposition. It marks the instance `Archived` and
unassigns its teacher. It creates no new row and does not touch enrollments, students, grades or notes. The
caller decides whether the `ClassGroup` also becomes `Ended`.

`RolloverService.GetClassRolloverStatesAsync` reports, for every class of a year, whether it still needs a
decision, how many students carry over, a suggested next name and its successor in the target year. A partly
finished rollover resumes from database state alone, because the guided end-of-year flow derives its resume
point from this.

Today `RolloverService.RollClassInstanceForwardAsync` (through `CopySubjectInstancesAsync`) is the only code
that creates a `SubjectInstance`.

## Teacher

A `Teacher` is in practice one row: the app owner (`IsCurrentUser`). It is an entity so that a future
handover is a data change, not a schema migration. `ClassInstance.TeacherId` and `SubjectInstance.TeacherId`
are optional.

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.Data/Entities/ClassGroup.cs`
- `NexGrades.Data/Entities/ClassInstance.cs`
- `NexGrades.Data/Entities/Teacher.cs`
- `NexGrades.Domain/Services/ClassService.cs`
- `NexGrades.Domain/Services/RolloverService.cs`
- `NexGrades.Domain/Services/ClassRolloverState.cs`

## Related

- [School year lifecycle](school-year-lifecycle.md)
- [Students and enrollment](students-and-enrollment.md)
- [Subjects, buckets and participation](subjects-buckets-and-participation.md)
- [Closed-year lock and audit](closed-year-lock-and-audit.md)
