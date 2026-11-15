# Students and enrollment

A student is a person; an enrollment places that person in one class for part of a school year.

## Student

A `Student` is a person. It is not year-scoped: the same row persists for the student's whole school career.
A student has a status (`Active` or `Left`). Students are changed to `Left`, never deleted.

`StudentService` creates a student and enrolls them in a class in one atomic operation, and renames students.

## Enrollment

An `Enrollment` is the membership of a student in a `ClassInstance`. `JoinedOn` is the first day; the
enrollment is active from that date on, inclusive. `LeftOn` is the day the student left; the enrollment is no
longer active from that date on, and it is null while the student is still enrolled. A student who left keeps
the enrollment row.

A student has at most one enrollment per school year. The rule spans every class instance of that year. The
database cannot express it as a unique constraint without a denormalized `SchoolYearId` column on
`Enrollment`, and the model deliberately has none (see
[Closed-year lock and audit](closed-year-lock-and-audit.md)). The schema only prevents an exact duplicate
(student, class instance) pair with a unique index. `EnrollmentInvariants` enforces the per-year rule in the
Domain, inside a transaction that resolves the class instance to its school year and checks every enrollment
of the student in that year. `EnrollmentService`, `StudentService` and the rollover all go through it.

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.Data/Entities/Student.cs`
- `NexGrades.Data/Entities/Enrollment.cs`
- `NexGrades.Domain/Services/StudentService.cs`
- `NexGrades.Domain/Services/EnrollmentService.cs`
- `NexGrades.Domain/Services/EnrollmentInvariants.cs`
- `NexGrades.Domain/Services/EnrollmentActivity.cs`

## Related

- [Classes, cohorts and rollover](classes-cohorts-and-rollover.md)
- [Subjects, buckets and participation](subjects-buckets-and-participation.md)
- [Notes](notes.md)
