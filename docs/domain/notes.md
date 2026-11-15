# Notes

A note is free text a teacher keeps about a student within one school year.

## Note

A `Note` is a free-standing note, independent of grades. `NoteService` creates, edits, archives, restores and
lists notes.

A note hangs off the `Student` and the `SchoolYear`, not off a class, so it survives the student changing
class during the year. To add a note, the student needs an enrollment in that year. `Note.ClassInstanceId`
records the class the note was written in, copied from that enrollment at creation. The enrollment's `LeftOn`
is ignored: a student who left the class later in the year still has history there.

`Note.StudentId` and `Note.SchoolYearId` are fixed at creation. The closed-year interceptor resolves a note's
year from `SchoolYearId`, so moving a note to another year or student would bypass the lock of the year it
came from.

A note is "removed" by archiving (`ArchivedOn`), which can be undone by restoring. Nothing is deleted. A note
is locked like everything else in a closed year; see [Closed-year lock and audit](closed-year-lock-and-audit.md).
An archived note cannot be edited until it is restored. The text and the optional category are trimmed and
length-limited by `NoteService`.

The notes export is described in [Exports](exports.md).

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.Data/Entities/Note.cs`
- `NexGrades.Domain/Services/NoteService.cs`

## Related

- [Students and enrollment](students-and-enrollment.md)
- [Closed-year lock and audit](closed-year-lock-and-audit.md)
- [Exports](exports.md)
