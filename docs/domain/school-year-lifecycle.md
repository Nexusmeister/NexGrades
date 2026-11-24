# School year lifecycle

A `SchoolYear` is the root scope of the model. This page covers its statuses, how the next year is created and
edited, how a year is closed, and how close readiness is reported.

## Statuses

A school year has a label (for example "2025/26") and runs from August to July. Its status moves
`Planned` to `Active` to `Closed`. At most one year is `Active` at a time. `Closed` is what makes the data
the year scopes read-only; see [Closed-year lock and audit](closed-year-lock-and-audit.md).

At startup `SchoolYearService.EnsureActiveYearAsync` creates an `Active` year when none exists. It creates
the August to July year that contains today. Planned and Closed years do not count, so an `Active` year is
created even when only those exist. It does nothing when a year is already `Active`.

## Creating the next year

`SchoolYearService.CreateNextYearAsync` creates the `Planned` successor of the `Active` year. Only one
`Planned` successor may exist at a time, so a repeated call cannot create a duplicate. The label must be
unused, the dates must be a valid range that starts after the previous year ends, and they must not overlap
any existing year.

`SchoolYearService.ProposeNextYearAsync` proposes the draft: the same dates one year later and the matching
label. It saves nothing. `SchoolYearService.FindPlannedSuccessorAsync` finds the earliest `Planned` year that
starts after a given year. The end-of-year flow uses it to resume after a restart.

## Editing the planned year

`SchoolYearService.UpdatePlannedYearAsync` changes the label, the start date and the end date of a `Planned`
year. It applies the same rules as `CreateNextYearAsync`: the label is not empty and is unused by another year,
the dates are a valid range, the range starts after the `Active` year ends, and it overlaps no other year. The
year being edited is left out of the label and overlap checks, so it may keep its own label or its old dates.

Only a `Planned` year can be edited. The `Active` year is running and a `Closed` year is history, so both are
refused, even while a `Closed` year is unlocked. An `Active` year must exist to check the new dates against.

The new dates must also keep every assessment and grade of the year inside them. Voided assessments and grades
count, so that un-voiding one later cannot leave it outside the year.

No `AuditEntry` is written. `SchoolYear` rows are outside the closed-year audit, and a `Planned` year is never
locked, so the edit needs no unlock. The id of the year does not change. `FindPlannedSuccessorAsync`, the class
rollover states and `YearCloseService.CloseAndActivateNextYearAsync` therefore still find and use the same year,
and the start stays after the `Active` year ends.

In the App, the School year page shows the planned year in its overview and in its next-year step. Once the
planned year exists, the overview has a "Next school year (planned)" card with the label and the dates and an
"Edit next school year" button (`EditNextYearCommand`), which opens the next-year step.

The next-year step is a form with the label and the start and end dates. All three are always editable, because
the Domain accepts a new label as well as new dates. Before the year exists, the step offers Create. Once it
exists, the step offers Save, Continue and Back instead. Save is enabled only when the typed values differ from
the saved year and pass the form's checks. It calls `UpdatePlannedYearAsync`, shows a success message and reloads
the flow, and the user stays on the step. A validation line under the form names what is wrong (an empty label,
missing dates, or an end date that is not after the start). A hint under the form says the year is planned and
can be changed until it becomes active. The Domain still checks overlaps and the other years when Save runs.

Leaving the step with unsaved edits asks first. This applies to Back, to Continue and to leaving the page by
the sidebar or the back button (`CanLeaveAsync`). Discarding restores the saved values, and keeping editing keeps
what was typed.

The step is reached from the start of the flow only when no planned year exists. When one exists, the flow
starts at the first unfinished step (the classes while any class is pending, otherwise the readiness check), and
Back from the classes returns to the next-year step.

## Closing a year

`YearCloseService.CloseAndActivateNextYearAsync` closes the `Active` year and activates the `Planned` next
year in a single transaction. A failure leaves both years unchanged, so there is never a moment without an
`Active` year. It sets `ClosedOn` on the closed year. The next year must start after the closed one ends, and no
blocking issue may remain.

It is the only way to close a year.

## Close readiness

`YearCloseService.GetCloseReadinessAsync` reports what would prevent a close (blocking) and what looks
unfinished but would not (advisory). Each finding is a structured `CloseReadinessIssue` with a
`CloseReadinessIssueKind`. The service never produces English sentences for them; the UI words them. The
ids in an issue are for linking only.

- `ClassStillActive` blocks: a class is neither rolled forward nor archived. See
  [Classes, cohorts and rollover](classes-cohorts-and-rollover.md).
- `SubjectWithoutGrades`, `BucketWeightsNotHundred` and `StudentWithoutGrade` are advisory.

A grade counts for readiness only while neither it nor its assessment is voided; see
[Grades, assessments and averages](grades-assessments-and-averages.md).

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.Data/Entities/SchoolYear.cs`
- `NexGrades.Domain/Services/SchoolYearService.cs`
- `NexGrades.Domain/Services/YearCloseService.cs`
- `NexGrades.App/Features/SchoolYears/SchoolYearViewModel.cs`
- `NexGrades.Domain/Services/CloseReadiness.cs`
- `NexGrades.Domain/Services/CloseReadinessIssue.cs`
- `NexGrades.Domain/Services/CloseReadinessIssueKind.cs`

## Related

- [Classes, cohorts and rollover](classes-cohorts-and-rollover.md)
- [Closed-year lock and audit](closed-year-lock-and-audit.md)
- [Grades, assessments and averages](grades-assessments-and-averages.md)
- [Startup, logging and data paths](startup-logging-and-data-paths.md)
