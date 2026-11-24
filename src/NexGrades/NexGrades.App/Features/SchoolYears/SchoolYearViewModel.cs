using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.EntityFrameworkCore;
using NexGrades.App.Core;
using NexGrades.App.Features.Classes;
using NexGrades.App.Features.Students;
using NexGrades.App.Localization;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;
using NexGrades.Domain.Time;
using Wpf.Ui;
using Wpf.Ui.Controls;
using Wpf.Ui.Extensions;

namespace NexGrades.App.Features.SchoolYears;

/// <summary>
/// Drives the guided "End school year" flow: create the next school year as a Planned year, roll forward or
/// archive each class, review what is unfinished, then close the Active year and activate the next one in one
/// step. Nothing about the flow is stored: after a restart or a visit to another page it resumes from what the
/// database says (the Planned successor, and which classes are done).
/// </summary>
/// <param name="navigator">Opens the class and student pages from the readiness step.</param>
/// <param name="navigation">Opens the classes overview after the flow is finished.</param>
/// <param name="snackbar">Reports outcomes in plain sentences.</param>
/// <param name="contentDialogService">Asks for confirmation before the steps that cannot be undone.</param>
/// <param name="clock">Source of "today", to tell whether the year has ended yet.</param>
/// <param name="schoolYearService">Finds the active and the next year and creates the next one.</param>
/// <param name="rolloverService">Reports the state of each class and rolls forward or archives it.</param>
/// <param name="yearCloseService">Reports readiness and closes the year.</param>
/// <param name="closedYears">The "Closed school years" section, reloaded on each visit and after a close.</param>
public sealed partial class SchoolYearViewModel(
    PageNavigator navigator,
    INavigationService navigation,
    ISnackbarService snackbar,
    IContentDialogService contentDialogService,
    IClock clock,
    SchoolYearService schoolYearService,
    RolloverService rolloverService,
    YearCloseService yearCloseService,
    ClosedYearsViewModel closedYears) : ViewModel
{
    /// <summary>The closed school years with their unlock controls.</summary>
    public ClosedYearsViewModel ClosedYears { get; } = closedYears;

    private readonly LeaveGuard _leaveGuard = new(contentDialogService);

    private SchoolYearSummary? _activeYear;
    private SchoolYearSummary? _nextYear;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(CreateNextYearCommand), nameof(ApplyClassChoicesCommand), nameof(ContinueToReadinessCommand), nameof(ContinueToConfirmCommand), nameof(RunCloseCommand), nameof(BackCommand), nameof(SaveNextYearCommand), nameof(ContinueToClassesCommand), nameof(EditNextYearCommand))]
    private bool _isBusy;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BackCommand), nameof(RunCloseCommand))]
    [NotifyPropertyChangedFor(nameof(IsOverviewStep), nameof(IsNextYearStep), nameof(IsClassesStep), nameof(IsReadinessStep), nameof(IsConfirmStep), nameof(IsResultStep))]
    private EndYearStep _currentStep = EndYearStep.Overview;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand))]
    private bool _hasActiveYear;

    [ObservableProperty]
    private string _activeYearLabel = string.Empty;

    [ObservableProperty]
    private DateTime? _activeYearStart;

    [ObservableProperty]
    private DateTime? _activeYearEnd;

    [ObservableProperty]
    private bool _isBeforeEndDate;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateNextYearCommand), nameof(SaveNextYearCommand))]
    [NotifyPropertyChangedFor(nameof(NextYearValidationMessage), nameof(HasNextYearValidationMessage))]
    private string _nextYearLabel = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateNextYearCommand), nameof(SaveNextYearCommand))]
    [NotifyPropertyChangedFor(nameof(NextYearValidationMessage), nameof(HasNextYearValidationMessage))]
    private DateTime? _nextYearStart;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateNextYearCommand), nameof(SaveNextYearCommand))]
    [NotifyPropertyChangedFor(nameof(NextYearValidationMessage), nameof(HasNextYearValidationMessage))]
    private DateTime? _nextYearEnd;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CreateNextYearCommand), nameof(SaveNextYearCommand), nameof(ContinueToClassesCommand), nameof(EditNextYearCommand))]
    private bool _isNextYearCreated;

    [ObservableProperty]
    private ObservableCollection<ClassChoiceRow> _classes = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBlockingItems))]
    [NotifyCanExecuteChangedFor(nameof(ContinueToConfirmCommand))]
    private ObservableCollection<ReadinessItemRow> _blockingItems = [];

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasWarningItems))]
    [NotifyCanExecuteChangedFor(nameof(ContinueToConfirmCommand))]
    private ObservableCollection<ReadinessItemRow> _warningItems = [];

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueToConfirmCommand))]
    private bool _areWarningsAcknowledged;

    [ObservableProperty]
    private string _summaryText = string.Empty;

    [ObservableProperty]
    private string _resultText = string.Empty;

    /// <summary>Whether the overview step is showing.</summary>
    public bool IsOverviewStep => CurrentStep == EndYearStep.Overview;

    /// <summary>Whether the next-year step is showing.</summary>
    public bool IsNextYearStep => CurrentStep == EndYearStep.NextYear;

    /// <summary>Whether the classes step is showing.</summary>
    public bool IsClassesStep => CurrentStep == EndYearStep.Classes;

    /// <summary>Whether the readiness step is showing.</summary>
    public bool IsReadinessStep => CurrentStep == EndYearStep.Readiness;

    /// <summary>Whether the confirmation step is showing.</summary>
    public bool IsConfirmStep => CurrentStep == EndYearStep.Confirm;

    /// <summary>Whether the result step is showing.</summary>
    public bool IsResultStep => CurrentStep == EndYearStep.Result;

    /// <summary>
    /// Why the next year's label and dates cannot be saved yet, or <see langword="null"/> when they can. Checks only
    /// what the form can know; the Domain still decides on overlaps and the other years.
    /// </summary>
    public string? NextYearValidationMessage
    {
        get
        {
            if (string.IsNullOrWhiteSpace(NextYearLabel))
            {
                return TextResources.Text("SchoolYears_NextYear_Validation_LabelRequired");
            }

            if (NextYearStart is not { } start || NextYearEnd is not { } end)
            {
                return TextResources.Text("SchoolYears_NextYear_Validation_DatesRequired");
            }

            return start.Date >= end.Date ? TextResources.Text("SchoolYears_NextYear_Validation_DateOrder") : null;
        }
    }

    /// <summary>Whether <see cref="NextYearValidationMessage"/> has something to show.</summary>
    public bool HasNextYearValidationMessage => NextYearValidationMessage is not null;

    /// <summary>Whether the typed label or dates differ from the saved Planned year; false before it exists.</summary>
    private bool HasUnsavedNextYearChanges =>
        _nextYear is { } saved
        && (!string.Equals(NextYearLabel.Trim(), saved.Label, StringComparison.Ordinal)
            || NextYearStart?.Date != saved.StartDate.Date
            || NextYearEnd?.Date != saved.EndDate.Date);

    /// <summary>Whether something blocks the close.</summary>
    public bool HasBlockingItems => BlockingItems.Count > 0;

    /// <summary>Whether there are advisory warnings.</summary>
    public bool HasWarningItems => WarningItems.Count > 0;

    /// <summary>
    /// Reloads the flow on every visit. A finished flow starts over at the overview; an unfinished one keeps its
    /// step and any unsaved class choices. A <see cref="SchoolYearPageRequest.StartEndOfYearFlow"/> request starts the
    /// flow, but only from the overview, so a flow that is already under way is never moved.
    /// </summary>
    protected override async Task OnNavigatedToAsync(object? parameter)
    {
        if (CurrentStep == EndYearStep.Result)
        {
            ResetFlow();
        }

        await ReloadAsync();
        await ClosedYears.ReloadAsync();

        if (parameter is SchoolYearPageRequest.StartEndOfYearFlow
            && CurrentStep == EndYearStep.Overview
            && StartCommand.CanExecute(null))
        {
            await StartCommand.ExecuteAsync(null);
        }
    }

    private void ResetFlow()
    {
        CurrentStep = EndYearStep.Overview;
        Classes = [];
        BlockingItems = [];
        WarningItems = [];
        AreWarningsAcknowledged = false;
        NextYearLabel = string.Empty;
        NextYearStart = null;
        NextYearEnd = null;
        SummaryText = string.Empty;
        ResultText = string.Empty;
        _nextYear = null;
        IsNextYearCreated = false;
    }

    /// <summary>Reads the active year, its successor and the class states, and merges them into the screen state.</summary>
    private async Task ReloadAsync()
    {
        IsBusy = true;
        try
        {
            var active = await schoolYearService.GetActiveYearAsync();
            if (_activeYear is not null && active?.Id != _activeYear.Id)
            {
                ResetFlow();
            }

            _activeYear = active;
            ApplyActiveYear(active);
            if (active is null)
            {
                return;
            }

            _nextYear = await schoolYearService.FindPlannedSuccessorAsync(active.Id);
            ApplyNextYear(_nextYear);

            var states = await rolloverService.GetClassRolloverStatesAsync(active.Id, _nextYear?.Id);
            MergeClassStates(states);

            if (CurrentStep == EndYearStep.Readiness)
            {
                await LoadReadinessAsync(active.Id);
            }
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            ShowError(TextResources.Text("SchoolYears_LoadFailed"));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyActiveYear(SchoolYearSummary? active)
    {
        HasActiveYear = active is not null;
        ActiveYearLabel = active?.Label ?? string.Empty;
        ActiveYearStart = active?.StartDate;
        ActiveYearEnd = active?.EndDate;
        IsBeforeEndDate = active is not null && clock.Now.Date < active.EndDate.Date;
    }

    private void ApplyNextYear(SchoolYearSummary? next)
    {
        IsNextYearCreated = next is not null;
        if (next is null)
        {
            return;
        }

        NextYearLabel = next.Label;
        NextYearStart = next.StartDate;
        NextYearEnd = next.EndDate;
        SaveNextYearCommand.NotifyCanExecuteChanged();
    }

    private void MergeClassStates(IReadOnlyList<ClassRolloverState> states)
    {
        var existing = Classes.ToDictionary(row => row.ClassInstanceId);
        var merged = new ObservableCollection<ClassChoiceRow>();
        foreach (var state in states)
        {
            if (existing.TryGetValue(state.ClassInstanceId, out var row))
            {
                row.Apply(state);
            }
            else
            {
                row = new ClassChoiceRow(state);
                row.PropertyChanged += (_, _) => ApplyClassChoicesCommand.NotifyCanExecuteChanged();
            }

            merged.Add(row);
        }

        Classes = merged;
        ApplyClassChoicesCommand.NotifyCanExecuteChanged();
        ContinueToReadinessCommand.NotifyCanExecuteChanged();
    }

    private async Task LoadReadinessAsync(int schoolYearId)
    {
        var readiness = await yearCloseService.GetCloseReadinessAsync(schoolYearId);
        BlockingItems = [.. readiness.BlockingIssues.Select(ToRow)];
        WarningItems = [.. readiness.Warnings.Select(ToRow)];
        AreWarningsAcknowledged = false;
    }

    private static ReadinessItemRow ToRow(CloseReadinessIssue issue)
    {
        var text = ReadinessItemText.Format(issue, CultureInfo.CurrentUICulture, CultureInfo.CurrentCulture);
        return issue.Kind == CloseReadinessIssueKind.StudentWithoutGrade && issue.StudentId is { } studentId
            ? new ReadinessItemRow(text, ReadinessLinkTarget.Student, studentId)
            : new ReadinessItemRow(text, ReadinessLinkTarget.Class, issue.ClassInstanceId);
    }

    private bool CanStart() => !IsBusy && HasActiveYear;

    /// <summary>Jumps to the first step that is not done yet.</summary>
    [RelayCommand(CanExecute = nameof(CanStart))]
    private async Task StartAsync()
    {
        await ReloadAsync();
        if (_activeYear is null)
        {
            return;
        }

        if (_nextYear is null)
        {
            await EnterNextYearStepAsync();
        }
        else if (Classes.Any(row => row.IsPending))
        {
            CurrentStep = EndYearStep.Classes;
        }
        else
        {
            await EnterReadinessStepAsync();
        }
    }

    private async Task EnterNextYearStepAsync()
    {
        CurrentStep = EndYearStep.NextYear;
        if (IsNextYearCreated || !string.IsNullOrWhiteSpace(NextYearLabel) || _activeYear is null)
        {
            return;
        }

        await RunGuardedAsync(async () =>
        {
            var draft = await schoolYearService.ProposeNextYearAsync(_activeYear.Id);
            NextYearLabel = draft.Label;
            NextYearStart = draft.StartDate;
            NextYearEnd = draft.EndDate;
        });
    }

    private async Task EnterReadinessStepAsync()
    {
        CurrentStep = EndYearStep.Readiness;
        if (_activeYear is null)
        {
            return;
        }

        await RunGuardedAsync(() => LoadReadinessAsync(_activeYear.Id));
    }

    private bool CanCreateNextYear() => !IsBusy && !IsNextYearCreated && NextYearValidationMessage is null;

    /// <summary>Creates the next year as a Planned year, then moves on to the classes.</summary>
    [RelayCommand(CanExecute = nameof(CanCreateNextYear))]
    private async Task CreateNextYearAsync()
    {
        if (_activeYear is null || NextYearStart is not { } start || NextYearEnd is not { } end)
        {
            return;
        }

        var created = await RunGuardedAsync(
            async () => _ = await schoolYearService.CreateNextYearAsync(_activeYear.Id, new SchoolYearDraft(NextYearLabel, start, end)),
            TextResources.Text("SchoolYears_NextYear_CreateFailed"));
        if (!created)
        {
            return;
        }

        await ReloadAsync();
        CurrentStep = EndYearStep.Classes;
    }

    private bool CanSaveNextYear() =>
        !IsBusy && IsNextYearCreated && HasUnsavedNextYearChanges && NextYearValidationMessage is null;

    /// <summary>Saves the changed label and dates of the Planned next year and stays on the step.</summary>
    [RelayCommand(CanExecute = nameof(CanSaveNextYear))]
    private async Task SaveNextYearAsync()
    {
        if (_nextYear is not { } nextYear || NextYearStart is not { } start || NextYearEnd is not { } end)
        {
            return;
        }

        var saved = await RunGuardedAsync(
            async () => _ = await schoolYearService.UpdatePlannedYearAsync(nextYear.Id, new SchoolYearDraft(NextYearLabel, start, end)),
            TextResources.Text("SchoolYears_NextYear_SaveFailed"));
        if (!saved)
        {
            return;
        }

        ShowSuccess(TextResources.Text("SchoolYears_NextYear_Saved"));
        await ReloadAsync();
    }

    private bool CanContinueToClasses() => !IsBusy && IsNextYearCreated;

    /// <summary>Moves from the next year to the classes, after asking what to do with unsaved edits.</summary>
    [RelayCommand(CanExecute = nameof(CanContinueToClasses))]
    private async Task ContinueToClassesAsync()
    {
        if (await ConfirmDiscardNextYearChangesAsync())
        {
            CurrentStep = EndYearStep.Classes;
        }
    }

    private bool CanEditNextYear() => !IsBusy && IsNextYearCreated;

    /// <summary>Opens the next-year step to edit the Planned year from the overview.</summary>
    [RelayCommand(CanExecute = nameof(CanEditNextYear))]
    private void EditNextYear() => CurrentStep = EndYearStep.NextYear;

    /// <summary>
    /// Asks before unsaved edits of the Planned year are dropped. When the user discards, the fields go back to the
    /// saved values; when they keep editing, the typed input stays.
    /// </summary>
    private async Task<bool> ConfirmDiscardNextYearChangesAsync(CancellationToken cancellationToken = default)
    {
        if (!await _leaveGuard.CanLeaveAsync(HasUnsavedNextYearChanges, cancellationToken))
        {
            return false;
        }

        if (HasUnsavedNextYearChanges && _nextYear is { } saved)
        {
            NextYearLabel = saved.Label;
            NextYearStart = saved.StartDate;
            NextYearEnd = saved.EndDate;
        }

        return true;
    }

    /// <inheritdoc/>
    public override Task<bool> CanLeaveAsync(CancellationToken cancellationToken = default) =>
        ConfirmDiscardNextYearChangesAsync(cancellationToken);

    private bool CanApplyClassChoices() =>
        !IsBusy
        && _nextYear is not null
        && Classes.Any(row => row.IsPending)
        && Classes.Where(row => row.IsPending).All(IsChoiceComplete);

    private static bool IsChoiceComplete(ClassChoiceRow row) => row.Choice switch
    {
        ClassYearEndChoice.RollForward => row.IsNewNameValid,
        ClassYearEndChoice.Archive => true,
        _ => false,
    };

    /// <summary>Applies the choice for every pending class, one class at a time. A class that fails is marked and the rest go on.</summary>
    [RelayCommand(CanExecute = nameof(CanApplyClassChoices))]
    private async Task ApplyClassChoicesAsync()
    {
        if (_nextYear is not { } nextYear || !await ConfirmAsync(
                TextResources.Text("SchoolYears_Classes_Apply_Title"),
                TextResources.Text("SchoolYears_Classes_Apply_Message"),
                TextResources.Text("SchoolYears_Action_Apply")))
        {
            return;
        }

        IsBusy = true;
        try
        {
            var pending = Classes.Where(row => row.IsPending).ToList();
            foreach (var row in pending)
            {
                row.HasFailed = !await TryApplyChoiceAsync(row, nextYear.Id);
            }

            await ReloadClassStatesAsync();
        }
        finally
        {
            IsBusy = false;
        }

        ReportClassOutcome();
        if (Classes.All(row => row.IsDone))
        {
            await EnterReadinessStepAsync();
        }
    }

    private async Task<bool> TryApplyChoiceAsync(ClassChoiceRow row, int nextYearId)
    {
        try
        {
            if (row.Choice == ClassYearEndChoice.RollForward)
            {
                _ = await rolloverService.RollClassInstanceForwardAsync(row.ClassInstanceId, nextYearId, row.NewName);
            }
            else
            {
                await rolloverService.ArchiveClassInstanceAsync(row.ClassInstanceId, row.EndClassGroup);
            }

            return true;
        }
        catch (Exception ex) when (ex is InvariantViolationException or ClosedYearException or DbUpdateException)
        {
            return false;
        }
    }

    private async Task ReloadClassStatesAsync()
    {
        try
        {
            if (_activeYear is not null)
            {
                MergeClassStates(await rolloverService.GetClassRolloverStatesAsync(_activeYear.Id, _nextYear?.Id));
            }
        }
        catch (Exception ex) when (ex is InvariantViolationException or DbUpdateException)
        {
            ShowError(TextResources.Text("SchoolYears_Classes_LoadFailed"));
        }
    }

    private void ReportClassOutcome()
    {
        if (Classes.Any(row => row.HasFailed))
        {
            ShowError(TextResources.Text("SchoolYears_Classes_SomeFailed"));
        }
        else
        {
            ShowSuccess(TextResources.Text("SchoolYears_Classes_Processed"));
        }
    }

    private bool CanContinueToReadiness() => !IsBusy && Classes.All(row => row.IsDone);

    /// <summary>Moves from the classes to the readiness check once every class is done.</summary>
    [RelayCommand(CanExecute = nameof(CanContinueToReadiness))]
    private async Task ContinueToReadinessAsync() => await EnterReadinessStepAsync();

    /// <summary>Opens the class or student a readiness item points at.</summary>
    [RelayCommand]
    private void OpenReadinessItem(ReadinessItemRow? item)
    {
        switch (item?.Target)
        {
            case ReadinessLinkTarget.Class:
                _ = navigator.NavigateWithHierarchy<ClassPage>(item.TargetId);
                break;
            case ReadinessLinkTarget.Student:
                _ = navigator.NavigateWithHierarchy<StudentPage>(item.TargetId);
                break;
            default:
                break;
        }
    }

    private bool CanContinueToConfirm() =>
        !IsBusy && !HasBlockingItems && (!HasWarningItems || AreWarningsAcknowledged);

    /// <summary>Moves from the readiness check to the final confirmation.</summary>
    [RelayCommand(CanExecute = nameof(CanContinueToConfirm))]
    private void ContinueToConfirm()
    {
        SummaryText = TextResources.Format(
            "SchoolYears_Confirm_Summary_Format",
            ActiveYearLabel,
            NextYearLabel,
            Classes.Count(row => row.Status == ClassInstanceStatus.RolledForward),
            Classes.Count(row => row.Status == ClassInstanceStatus.Archived));
        CurrentStep = EndYearStep.Confirm;
    }

    private bool CanRunClose() => !IsBusy && CurrentStep == EndYearStep.Confirm && _nextYear is not null;

    /// <summary>Closes the Active year and activates the next one in one step, after a last confirmation.</summary>
    [RelayCommand(CanExecute = nameof(CanRunClose))]
    private async Task RunCloseAsync()
    {
        if (_activeYear is not { } active || _nextYear is not { } next || !await ConfirmAsync(
                TextResources.Text("SchoolYears_Action_EndYear"),
                TextResources.Text("SchoolYears_Close_Confirm_Message"),
                TextResources.Text("SchoolYears_Action_EndYear")))
        {
            return;
        }

        var closed = await RunGuardedAsync(() => yearCloseService.CloseAndActivateNextYearAsync(active.Id, next.Id));
        if (!closed)
        {
            await ReloadAsync();
            CurrentStep = EndYearStep.Classes;
            return;
        }

        ResultText = TextResources.Format("SchoolYears_Close_Result_Format", active.Label, next.Label);
        CurrentStep = EndYearStep.Result;
        ShowSuccess(TextResources.Text("SchoolYears_Close_Success"));
        await ClosedYears.ReloadAsync();
    }

    private bool CanGoBack() => !IsBusy && CurrentStep is not (EndYearStep.Overview or EndYearStep.Result);

    /// <summary>Goes back one step.</summary>
    [RelayCommand(CanExecute = nameof(CanGoBack))]
    private async Task BackAsync()
    {
        switch (CurrentStep)
        {
            case EndYearStep.NextYear:
                if (await ConfirmDiscardNextYearChangesAsync())
                {
                    CurrentStep = EndYearStep.Overview;
                }

                break;
            case EndYearStep.Classes:
                CurrentStep = EndYearStep.NextYear;
                break;
            case EndYearStep.Readiness:
                CurrentStep = EndYearStep.Classes;
                break;
            case EndYearStep.Confirm:
                await EnterReadinessStepAsync();
                break;
            default:
                break;
        }
    }

    /// <summary>After the flow is finished, opens the classes overview, which now shows the new year's classes.</summary>
    [RelayCommand]
    private void GoToClasses()
    {
        _ = navigation.Navigate(typeof(ClassesOverviewPage));
    }

    /// <summary>Runs a Domain call that can reject the request; a rejection becomes a generic snackbar message.</summary>
    private async Task<bool> RunGuardedAsync(Func<Task> action, string? errorMessage = null)
    {
        IsBusy = true;
        try
        {
            await action();
            return true;
        }
        catch (Exception ex) when (ex is InvariantViolationException or ClosedYearException or DbUpdateException)
        {
            ShowError(errorMessage ?? TextResources.Text("SchoolYears_Close_Failed"));
            return false;
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<bool> ConfirmAsync(string title, string content, string confirmText)
    {
        var result = await contentDialogService.ShowSimpleDialogAsync(
            new SimpleContentDialogCreateOptions
            {
                Title = title,
                Content = content,
                PrimaryButtonText = confirmText,
                CloseButtonText = TextResources.Text("Common_Cancel"),
            });

        return result == ContentDialogResult.Primary;
    }

    private void ShowError(string message) =>
        snackbar.Show(TextResources.Text("Common_Error"), message, ControlAppearance.Caution, new SymbolIcon(SymbolRegular.ErrorCircle24), TimeSpan.FromSeconds(5));

    private void ShowSuccess(string message) =>
        snackbar.Show(TextResources.Text("Common_Success"), message, ControlAppearance.Success, new SymbolIcon(SymbolRegular.CheckmarkCircle32), TimeSpan.FromSeconds(5));
}
