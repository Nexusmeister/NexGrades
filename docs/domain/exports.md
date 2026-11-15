# Exports

NexGrades builds two export tables in the Domain. The App turns them into Excel files. Both are read-only, so
they also work on closed years.

## Class overview export

`SubjectOverviewExportService.BuildAsync` builds the overview of one subject instance: a row per student with
the grades and average of each bucket, the weighted subject average and the final grade.

- The final grade is the effective one: the teacher's override, otherwise the proposal, and empty when neither
  exists. See [Grading scale and final grade](grading-scale-and-final-grade.md).
- The Domain sets `SubjectOverviewRow.IsFinalGradeOverridden` when the final grade is an override. The App writes `Yes` in the "Final grade set by teacher" column for those rows and leaves the cell empty otherwise.
  The column header is written by `SubjectOverviewExcelWriter` in the App.
- Exempt students are not listed, even if they have grades.
- Students who left the class appear only if they have grades in this subject or a final-grade override.
- Voided grades and grades of voided assessments are excluded. See
  [Grades, assessments and averages](grades-assessments-and-averages.md).

Averages use the snapshotted `GradeBucket.Weight`.

## Notes export

`NoteExportService.BuildAsync` collects one student's notes in a date range, oldest first. The range may span
several school years.

- The range filters on `Note.CreatedOn`. `from` is inclusive and the whole `to` day is included.
- Archived notes are left out unless `includeArchived` is set.
- A reversed range, a range that ends on the last supported date, or an unknown student is rejected with an
  `InvariantViolationException`.

See [Notes](notes.md).

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.Domain/Export/SubjectOverviewExportService.cs`
- `NexGrades.Domain/Export/NoteExportService.cs`
- `NexGrades.App/Features/Export/SubjectOverviewExcelWriter.cs`
- `NexGrades.App/Features/Export/NoteExcelWriter.cs`

## Related

- [Grades, assessments and averages](grades-assessments-and-averages.md)
- [Grading scale and final grade](grading-scale-and-final-grade.md)
- [Notes](notes.md)
