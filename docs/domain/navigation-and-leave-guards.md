# Navigation and leave guards

A page with unsaved input must not lose it without asking. The app checks the current page before the user
leaves it by the sidebar, the Back button, the back gestures or by closing the window. The checks are part of
the App (`NexGrades.App/Core` and `NexGrades.App/Shell`). They are not business rules, and the guard saves
nothing itself.

## Asking before a page is left

`ViewModel.CanLeaveAsync` is asked before the user leaves a page. Its default allows leaving. A view model with
unsaved input overrides it, asks the user, and returns `false` to keep the user on the page.

The question is `DiscardChangesDialog.ConfirmAsync`, with the shared text `DiscardChangesDialog.UnsavedChangesMessage`.
Its buttons are "Discard" and "Keep editing".

Form pages use `LeaveGuard` (`NexGrades.App/Core/LeaveGuard.cs`), which holds the check and the allow-leave flag:

- `CanLeaveAsync(hasUnsavedChanges)` returns `true` at once when the page has decided to leave or nothing is
  unsaved. Otherwise it asks.
- `Reset()` re-arms the flag. A page calls it when it is shown.
- `LeaveWithoutPrompt(goBack)` sets the flag and goes back. A page that has just saved uses it, so the guard does
  not ask a second time. If going back fails, the flag is cleared again.
- `CancelAsync(hasUnsavedChanges, goBack)` is the page's Cancel: it asks when needed, then goes back.

`ClassViewModel` and `SubjectDetailViewModel` use `LeaveGuard`. `StudentViewModel` and `GradeSheetViewModel` do not.
Each keeps its own allow-leave flag and its own dialog helper. `StudentViewModel` uses the same wording as the shared
text, and `GradeSheetViewModel` words its own. A new form page should use `LeaveGuard`.

## Unsaved grid edits

A `DataGrid` cell that is still being edited is not yet in the view model. A page with a grid sets
`ViewModel.CommitPendingInput`, a `Func<bool>`. `PageNavigator` calls it before `CanLeaveAsync` on every guarded
leave, so the typed value reaches the view model and is then judged as unsaved input. `GradeSheetPage` commits the
grid's edit row this way. If the grid rejects the value, the callback returns `false` and the user stays on the page.

## What is guarded

- **Sidebar:** the shell's `Navigating` handler in `MainWindow.xaml.cs` calls `PageNavigator.ShouldCancelNavigating`.
- **Back button:** `GuardedNavigationView` calls `PageNavigator.TryGuardBack` before its own back navigation.
- **Alt+Left and the mouse back button:** `MainWindow.OnBrowseBackExecuting` (`Shell/MainWindow.LeaveGuards.cs`)
  previews `NavigationCommands.BrowseBack`. Any gesture that runs that command is guarded. The App binds no
  Backspace key itself.
- **Window close:** `MainWindow.OnClosing` calls `PageNavigator.ConfirmExitAsync`. See below.

When the check has to ask, the navigation is cancelled. It runs again only if the user agrees. While a prompt is
open, further navigation or close requests are refused without a second prompt. A check that throws keeps the user
on the page, and the error is logged without page content.

## Closing the window and "Restart now"

`PageNavigator.ConfirmExitAsync` asks the current page once. When the user agrees, the navigator remembers it in
`ExitConfirmed`, and later checks in the same run do not ask again. The window close and the update notice's
Restart now (`UpdateNoticeViewModel`) both use it, so the user is asked at most once. A refusal is not remembered,
so the next close asks again.

A confirmed close still ends in `App.OnExit`, which first calls `UpdateCoordinator.ApplyPendingOnExit`. A downloaded
update is therefore installed on exit. A refused close keeps the window open. See
[Updates, backups and settings](updates-backups-and-settings.md).

## Passing a parameter to the next page

`PageNavigator.NavigateWithHierarchy<TPage>(parameter)` stores the parameter for the target view model
(`NavigationParameters`), then navigates. The view model receives it once, in `OnNavigatedToAsync(object?)`. The
parameter is `null` when the page is opened without one, such as an Add flow.

Each stored value is versioned. If the navigation is vetoed, the value is discarded, and the re-issued navigation
stores it again. After a navigation that happened, a deferred cleanup removes the value if the view model did not
take it. The cleanup removes only the value its own call stored, so a newer value for the same page stays.

## Known behaviour

- **Windows logoff or shutdown:** the App does not handle the session-end event. The close check can therefore show
  the discard prompt while Windows is ending the session, and the prompt can hold the shutdown until the user
  answers. The owner decided to keep the prompt.

## Rules

See [Rules by area](../../AGENTS.md#rules-by-area) in `AGENTS.md`.

## Code

- `NexGrades.App/Core/ViewModel.cs`
- `NexGrades.App/Core/PageNavigator.cs`
- `NexGrades.App/Core/LeaveGuard.cs`
- `NexGrades.App/Core/DiscardChangesDialog.cs`
- `NexGrades.App/Core/NavigationParameters.cs`
- `NexGrades.App/Shell/MainWindow.LeaveGuards.cs`
- `NexGrades.App/Shell/GuardedNavigationView.cs`
- `NexGrades.App/Updates/UpdateNoticeViewModel.cs`

## Related

- [Updates, backups and settings](updates-backups-and-settings.md)
