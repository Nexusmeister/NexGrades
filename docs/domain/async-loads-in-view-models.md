# Async loads in view models

A view model that loads data asynchronously can have more than one load in flight: a filter toggle, a year change
or a second visit starts a new load before the first one has finished. The older load can finish last. It must not
overwrite the newer result, and it must not swallow what the user did meanwhile. This page describes the pattern the
App uses. It is App-side behaviour in `NexGrades.App`; no business rule is involved.

## Why

Reviews found two bugs of this kind:

- PR #205: overlapping loads on a page, where a slower older load replaced the result of a newer one.
- PR #207: a "Not now" on the Home reminder card, pressed while the reminder was still loading, was lost and the card
  came back.

Both come from the same cause: the code assumed one load at a time.

## Latest request wins

Each load takes a number from a counter and writes its result only while that number is still the newest:

```csharp
var version = ++_loadVersion;
// ... await the read ...
if (version != _loadVersion)
{
    return;
}
// ... assign the result ...
```

Rules:

- Take the version at the start of the load, before the first `await`, and take it on every path that starts a load.
- After each `await` whose result the view model assigns, check `version != _loadVersion` and return before
  assigning anything. A superseded run does not assign its result. A step that only re-applies the current state,
  such as `ApplyWritability()` after `await YearLock.ShowAsync(...)`, is harmless when repeated and may run
  without the check.
- A `finally` that clears a busy flag (`IsLoading`, `_isRefreshing`) clears it only when `version == _loadVersion`,
  so an old run does not switch the flag off while a newer run is still loading.
- A catch that changes state (hiding a card, clearing a list, showing an error) does so only when
  `version == _loadVersion`. The failure is still logged for every run.

Reference implementations, read them before writing a new one:

- `NexGrades.App/Features/Grades/GradeSheetViewModel.cs` (`LoadAsync`): the version guard, the busy flag in `finally`,
  and a failure that clears the sheet and shows an error only for the newest run.
- `NexGrades.App/Features/Students/StudentGradesViewModel.cs` (`ReloadAsync`): the same pattern for a reload that
  replaces only the list and keeps what the user typed in the editor.
- `NexGrades.App/Features/Classes/ClassesOverviewViewModel.cs` (`LoadClassesAsync`): a `[RelayCommand]` load, started
  by a filter toggle or a year change, with cancellation.
- `NexGrades.App/Features/Home/HomeViewModel.cs` (`RefreshYearEndReminderAsync`): a refresh that both navigation and
  the page's `Loaded` event start, plus a user action during the load.

## A user action during a load is remembered

When the user acts while a load is in flight, the action must be applied to the result of that load, not lost and
not overwritten by it. `HomeViewModel` shows how:

- `_dismissedYearId` holds the year the teacher dismissed with "Not now". It lasts until the next app start and is
  never logged.
- `_reminderYearId` is the year the card currently shows. `_isRefreshing` is set while a refresh is in flight.
- If "Not now" is pressed while a refresh is in flight and `_reminderYearId` is still unknown (the first refresh),
  there is no year id to record. `DismissYearEndReminder` sets `_dismissPending` instead and hides the card.
- When that refresh finishes, and it is still the newest run, it reads `_dismissPending`, clears it, and stores the
  loaded reminder's year id in `_dismissedYearId`. The card then stays hidden.
- If a year id is already known, "Not now" records it in `_dismissedYearId` at once. A refresh that is in flight
  compares its result with `_dismissedYearId` and keeps the card hidden for that year.
- A superseded run returns before it reads `_dismissPending`, so the flag stays set for the newer run. A failed read
  of the newest run clears the flag, because there is no result to apply it to.

The general rule: store the action in a form that does not depend on the result (a flag, or an id that is already
known), and apply it when the newest result arrives.

## Cancelled and failed runs

**Superseded `[RelayCommand]` runs.** An async `[RelayCommand]` that takes a `CancellationToken` is cancelled by the
toolkit when the command runs again. The cancelled run ends in `OperationCanceledException`. Catch it quietly, with
nothing logged and nothing shown, because a newer run has taken over; and the toolkit must not see the exception.
`ClassesOverviewViewModel.LoadClassesAsync` has `catch (OperationCanceledException)` before its general catch.

**Other catches.** A catch of `Exception` that is not meant to handle cancellation is written
`catch (Exception ex) when (ex is not OperationCanceledException)`, as in `GradeSheetViewModel`,
`StudentGradesViewModel` and `HomeViewModel`. Where a view model has its own `catch (OperationCanceledException)` first,
as `ClassesOverviewViewModel` does, a plain `catch (Exception ex)` after it is enough.

**Failed reads.** A read can fail in more ways than a write. Besides `DbUpdateException` and
`InvariantViolationException`, a read can throw `DbException` (such as `SqliteException`) or
`InvalidOperationException`. A load therefore catches `Exception`, not a list of types, and:

- logs the failure with a `[LoggerMessage]` source-generated method at `LogLevel.Warning` with a fixed message,
  such as `LogReminderFailed` in `HomeViewModel` and `LogLoadFailed` in `ClassesOverviewViewModel`;
- passes the exception as the exception argument, and never puts `ex.Message`, names, grades, notes, record ids or
  school-year labels into the message (see [Startup, logging and data paths](startup-logging-and-data-paths.md)
  and `docs/decisions/0003-application-logging.md`);
- keeps the page usable: `HomeViewModel` hides the reminder card without a message, `ClassesOverviewViewModel` keeps
  the list it already shows, and `GradeSheetViewModel` and `StudentGradesViewModel` clear their content and show an
  error through the snackbar.

`GradeSheetViewModel` and `StudentGradesViewModel` log load failures at `Error`, because the teacher asked for that
data and sees an error. A convenience read, or a reload that leaves the old list in place, logs at `Warning`. A load
that is fire-and-forget (the Home refresh) must not let an exception escape as an unobserved faulted task, which is
why it catches `Exception`.

## Testing

Tests are deterministic and use no sleeps or delays.

- `NexGrades.App.Tests/TestSupport/QueryGatedContextFactory.cs` creates contexts on the given options and can hold
  back the next query right after the database has answered it, whatever its cancellation token says.
  `HoldNextQueryAsync(out var release)` returns a task that completes once a query is held. Await it, do the
  second action, call `release()`, then await the older run. This freezes the older run with the answer it already
  has, so it finishes after the newer one without any timing.
- `ClassesOverviewViewModelTests` holds an older load, supersedes it through the real command (a year change or a
  filter toggle), releases it and asserts that the newest list, year and toggle remain and that no warning was
  logged. It waits for the newer run with `LockTestEnvironment.WaitUntilAsync`, a condition poll rather than a fixed
  delay.
- `HomeViewModelTests` holds the first refresh, presses "Not now" (`DismissYearEndReminderCommand`), releases it and
  asserts the card stays hidden, also on a later refresh. A second test changes the database (closes the year) while an
  older refresh is held, runs a newer refresh, then releases the older one and asserts the card does not return.
- Failed reads are tested with a factory that throws (`FailingFactory`, `FlakyContextFactory`) and a
  `CapturingLogger<T>`. The tests assert one `Warning` entry with an exception, and that the entry's message holds no
  record content.
- Test the version guard on its own: one test that holds an older run and lets a newer run finish first, with the
  assertion on the newer result. A test where the runs happen to finish in order does not prove the guard.

## Checklist

For every view model that loads asynchronously or reloads on a toggle, a year change or a navigation:

- [ ] The load takes `var version = ++_loadVersion;` before its first `await`.
- [ ] After each `await` whose result is assigned, `version != _loadVersion` returns before the assignment.
- [ ] Busy flags and error states change only for `version == _loadVersion`.
- [ ] An action the user can take during a load is remembered and applied to the newest result.
- [ ] A cancelled `[RelayCommand]` run is caught quietly; other catches use `when (ex is not OperationCanceledException)`.
- [ ] A failed read is caught as `Exception`, logged through a `[LoggerMessage]` method at `Warning` (or `Error` when
      the teacher sees an error) with the exception as argument and no record content.
- [ ] A test holds an older load with `QueryGatedContextFactory`, lets the newer one finish first, and checks the
      result, the user action and the log.

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md` for the logging rules and the DbContext lifetime
rule that these loads follow.

## Code

- `NexGrades.App/Features/Grades/GradeSheetViewModel.cs`
- `NexGrades.App/Features/Students/StudentGradesViewModel.cs`
- `NexGrades.App/Features/Classes/ClassesOverviewViewModel.cs`
- `NexGrades.App/Features/Home/HomeViewModel.cs`
- `NexGrades.App.Tests/TestSupport/QueryGatedContextFactory.cs`

## Related

- [Navigation and leave guards](navigation-and-leave-guards.md)
- [Startup, logging and data paths](startup-logging-and-data-paths.md)
- [Grades, assessments and averages](grades-assessments-and-averages.md)
