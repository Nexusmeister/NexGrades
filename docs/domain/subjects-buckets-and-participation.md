# Subjects, buckets and participation

This page covers the subject catalogue, the bucket layout of a subject, how a subject is taught to a class in
one year, and which students take part.

## Subject and BucketTemplate

A `Subject` is a catalogue entry (for example "Mathematik") with a name and a short code. It is not
year-scoped. `SubjectService.CreateSubjectAsync` adds subjects to the catalogue. `Subject.IsActive` is stored
only; no Domain or App code reads it yet.

A `Subject` owns `BucketTemplate` rows: its default bucket layout, each with a name, a weight and a sort
order. A template is copied into a `GradeBucket` whenever a subject instance is created. Editing a template
afterwards affects only subject instances created afterwards.

No Domain or App code creates or edits templates today; they are read by the rollover. Editing templates is
the model's intent, not a current feature.

## SubjectInstance

A `SubjectInstance` is a subject taught to one `ClassInstance` in one year. Each subject appears at most once
per class instance (unique index). It has an optional teacher, its `GradeBucket`s and its
`SubjectParticipation` overrides. Today the rollover is the only code that creates a subject instance; see
[Classes, cohorts and rollover](classes-cohorts-and-rollover.md).

## GradeBucket snapshots

A `GradeBucket` is a snapshot. It is copied from the subject's `BucketTemplate`s when the `SubjectInstance`
is created, and is then independently editable (the model's intent; nothing edits a bucket yet). It
deliberately has no foreign key back to the template. That independence is the point: editing a template
later must never reach backwards into a past year and silently change a computed average.

The subject average uses the snapshotted `GradeBucket.Weight`, never the live `BucketTemplate.Weight`. See
[Grades, assessments and averages](grades-assessments-and-averages.md).

## SubjectParticipation

Default participation follows class enrollment: an enrolled student takes part. A `SubjectParticipation` is
the per-student override of that rule (an elective, a religion/ethics split, an exemption). Rows exist only
for deviations, so the common case has no rows.

`Mode` is `Participating` or `Exempt`. An exempt student cannot receive a grade or a final grade in that
subject instance. The grade sheet shows no averages for them, and the class overview export leaves them
out. `From` and `To` describe the period of the override. The rollover copies them unchanged, and no Domain
rule checks them yet.

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.Data/Entities/Subject.cs`
- `NexGrades.Data/Entities/BucketTemplate.cs`
- `NexGrades.Data/Entities/SubjectInstance.cs`
- `NexGrades.Data/Entities/GradeBucket.cs`
- `NexGrades.Data/Entities/SubjectParticipation.cs`
- `NexGrades.Data/Entities/ParticipationMode.cs`
- `NexGrades.Domain/Services/SubjectService.cs`
- `NexGrades.Domain/Services/RolloverService.cs`

## Related

- [Classes, cohorts and rollover](classes-cohorts-and-rollover.md)
- [Grades, assessments and averages](grades-assessments-and-averages.md)
- [Grading scale and final grade](grading-scale-and-final-grade.md)
