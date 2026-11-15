# Grading scale and final grade

This page covers the grading scale, how grades are written and parsed, how the final grade is proposed, and
how a teacher overrides it.

## The scale

The scale is the German 1 to 6 scale with tendencies, defined in `NexGrades.Domain/Grading/GradingScale.cs`.
It has 16 legal values, from 1+ (0.7) to 6 (6.0). Grades are stored as `decimal` in `Grade.Value`, and
**lower is better**. The schema stores a plain `decimal`; that a value is one of the 16 legal points is
application logic, checked by `GradingScale.Validate`.

| Grade | Plus | Plain | Minus |
|---|---|---|---|
| 1 | 1+ = 0.7 | 1 = 1.0 | 1- = 1.3 |
| 2 | 2+ = 1.7 | 2 = 2.0 | 2- = 2.3 |
| 3 | 3+ = 2.7 | 3 = 3.0 | 3- = 3.3 |
| 4 | 4+ = 3.7 | 4 = 4.0 | 4- = 4.3 |
| 5 | 5+ = 4.7 | 5 = 5.0 | 5- = 5.3 |
| 6 | none | 6 = 6.0 | none |

The scale is fixed to 1 to 6 for now. Scale-specific knowledge stays inside `GradingScale`, so that another
scale can be added later without touching callers.

## Tokens and display

`GradingScale` maps tokens to values and back. `TryParseToken` parses typed input such as "2-", " 1+ " or
"2 -": whitespace is ignored and the rest must be exactly one token. `TokenForValue` returns the token for a
value, and throws for a value that is not on the scale. Callers never parse tokens themselves. Tokens are
culture-neutral.

`AverageCalculator.RoundForDisplay` rounds an average to 2 decimals, with a midpoint rounded away from zero
(2.345 becomes 2.35). It is the number the UI shows.

## Proposed final grade

`GradingScale.ProposeFinalGrade` proposes the final grade from the subject average. It first rounds the
average to 2 decimals with `AverageCalculator.RoundForDisplay`, so the proposal matches the number the
teacher sees. It then rounds to a whole grade from 1 to 6, with .5 going to the worse grade: 2.50 gives 3,
2.49 gives 2. It is `null` when there is no average. It is derived, never stored, because a stored proposal
could drift from the grades.

A final grade is a whole grade with no tendency, so "2-" is not a final grade. `GradingScale.FinalGradeValues`
lists the six legal values, and `GradingScale.ValidateFinalGrade` and `GradingScale.TryParseFinalGradeToken`
check them.

## Final grade override

A `FinalGrade` is the teacher's override of a student's whole-grade final grade in one `SubjectInstance`. At
most one row exists per subject instance and student. Only `OverrideValue` is stored. A row exists only once
the teacher has set an override.

- The row is created on the first override.
- Clearing sets `OverrideValue` to `null` instead of deleting the row, because nothing is ever deleted. A row
  with no override means "use the proposal".
- The effective final grade is the override when one is set, otherwise the proposal.
- An exempt student has no proposal and no override on the grade sheet, even if a row exists from before the
  exemption.

`GradeService.SetFinalGradeOverrideAsync` sets the override and `GradeService.ClearFinalGradeOverrideAsync`
clears it. Setting needs an enrollment of the student in the subject instance's class, but checks it without a
date: a final grade covers the whole year, so a student who has left can still get one. It rejects an exempt
student.

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.Domain/Grading/GradingScale.cs`
- `NexGrades.Domain/Grading/AverageCalculator.cs`
- `NexGrades.Data/Entities/FinalGrade.cs`
- `NexGrades.Domain/Services/GradeService.cs`

## Related

- [Grades, assessments and averages](grades-assessments-and-averages.md)
- [Subjects, buckets and participation](subjects-buckets-and-participation.md)
- [Exports](exports.md)
