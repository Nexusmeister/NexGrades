# Grades, assessments and averages

This page covers grades, assessments, how voiding works, how averages are computed and how a student's
grades are shown on the student page.

## Assessment

An `Assessment` is one graded occasion in a `GradeBucket`, such as a class test or an oral check, that many
students receive a grade for. On the grade sheet it is one column. It has a title, a date and `VoidedOn`.

Its bucket never changes after creation. Every linked grade must count toward the same bucket as its
assessment, and the closed-year interceptor resolves an assessment's year through its bucket, so moving an
assessment would move it out from under the lock of the year it came from.

An assessment is "removed" by voiding, which can be undone, like `Note.ArchivedOn` on a note. Nothing is
deleted. `AssessmentService` creates, edits, voids and restores assessments. Editing the date moves the
active grades that still carry the old date to the new one; grades whose date was edited by hand keep it.

## Grade

A `Grade` is one numeric grade in a bucket for a student. `AssessmentId` is null for a single grade with no
assessment. Otherwise the grade's bucket is always the assessment's bucket: the bucket is taken from the
assessment when the grade is recorded. The schema cannot express this, so the Domain keeps it. A partial
unique index allows at most one active grade per student and assessment.

A grade is "removed" by voiding (`VoidedOn`), which can be undone. `GradeService` records, changes, voids and
restores grades. It checks that the student was enrolled in the bucket's class on the grade's date, and that
the student is not exempt. The grade value must be on the scale; see
[Grading scale and final grade](grading-scale-and-final-grade.md).

## Voiding and what counts

A grade counts only while neither it nor its assessment is voided. Voided grades and grades of voided
assessments are excluded from averages, the class overview export and close readiness. Voiding an assessment
does not change its grades' own voided state, so restoring the assessment brings back the grades that were
not voided themselves.

## Averages

`AverageCalculator` is pure and works on `decimal`.

- `BucketAverage` is the plain mean of one student's grade values in a bucket.
- `SubjectAverage` is the sum of each bucket average times its weight, divided by the sum of the weights.
  Only buckets that have at least one grade take part.
- An empty bucket or subject has no average (`null`), not zero.

The weights are the snapshotted `GradeBucket.Weight`, never the live `BucketTemplate.Weight`; see
[Subjects, buckets and participation](subjects-buckets-and-participation.md). `AverageService` loads the data
and delegates to `AverageCalculator`. The grade sheet uses `GradeSheetAverages`.

## Student Grades tab

The Grades tab on the student page reads a student's grades for one school year through
`GradeSheetService.GetStudentGradesAsync`. It returns a `StudentGradeOverview` with one `StudentSubjectGrades`
per subject of the student's class, each with its buckets, averages and the final grade. A student who is not
enrolled in that year has no subjects.

An exempt subject lists its grades without averages and without a final grade. The tab shows the final grade
read-only, marked when it is an override. It corrects grades through `GradeService`, so the same rules apply
as on the grade sheet.

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.Data/Entities/Assessment.cs`
- `NexGrades.Data/Entities/Grade.cs`
- `NexGrades.Domain/Services/AssessmentService.cs`
- `NexGrades.Domain/Services/GradeService.cs`
- `NexGrades.Domain/Services/GradeSheetService.cs`
- `NexGrades.Domain/Services/AverageService.cs`
- `NexGrades.Domain/Grading/AverageCalculator.cs`
- `NexGrades.Domain/Grading/StudentSubjectGrades.cs`
- `NexGrades.Domain/Grading/StudentGradeOverview.cs`

## Related

- [Grading scale and final grade](grading-scale-and-final-grade.md)
- [Subjects, buckets and participation](subjects-buckets-and-participation.md)
- [Closed-year lock and audit](closed-year-lock-and-audit.md)
- [Exports](exports.md)
- [Notes](notes.md)
