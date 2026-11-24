using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core;
using NexGrades.App.Core.YearLock;
using NexGrades.App.Features.SchoolYears;
using NexGrades.App.Localization;
using NexGrades.App.Tests.TestSupport;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;
using Wpf.Ui.Controls;

namespace NexGrades.App.Tests.Features.SchoolYears;

public class SchoolYearViewModelTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0);
    private static readonly DateTime NextStart = new(2026, 8, 1);
    private static readonly DateTime NextEnd = new(2027, 7, 31);

    private sealed record Harness(
        SchoolYearViewModel ViewModel, FakeContentDialogService Dialog, LockTestEnvironment Env);

    private static Harness Create(LockTestEnvironment env, ContentDialogResult answer = ContentDialogResult.None)
    {
        var factory = new YearLockViewModelFactory(
            env.Monitor, new FakeUnlockDialog(), env.Snackbar, env.Clock, NullLogger<YearLockViewModel>.Instance);
        var closedYears = new ClosedYearsViewModel(env.Monitor, factory, env.Snackbar);
        var (dialogService, dialog) = FakeContentDialogService.Create(answer);
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var viewModel = new SchoolYearViewModel(
            navigator,
            FakeNavigationService.Create(),
            env.Snackbar,
            dialogService,
            env.Clock,
            new SchoolYearService(env.Factory, env.Clock, NullLogger<SchoolYearService>.Instance),
            new RolloverService(env.Factory, env.Clock),
            new YearCloseService(env.Factory, env.Clock, NullLogger<YearCloseService>.Instance),
            closedYears);
        return new Harness(viewModel, dialog, env);
    }

    private static async Task SeedActiveAsync(LockTestEnvironment env)
    {
        await using var context = new AppDbContext(env.Options);
        context.SchoolYears.Add(new SchoolYear
        {
            Label = "2025/26",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = SchoolYearStatus.Active,
        });
        await context.SaveChangesAsync();
    }

    private static async Task<int> SeedActiveAndPlannedAsync(LockTestEnvironment env)
    {
        await SeedActiveAsync(env);
        await using var context = new AppDbContext(env.Options);
        var planned = new SchoolYear { Label = "2026/27", StartDate = NextStart, EndDate = NextEnd, Status = SchoolYearStatus.Planned };
        context.SchoolYears.Add(planned);
        await context.SaveChangesAsync();
        return planned.Id;
    }

    private static async Task<SchoolYear> LoadAsync(LockTestEnvironment env, int id)
    {
        await using var context = new AppDbContext(env.Options);
        return await context.SchoolYears.AsNoTracking().SingleAsync(sy => sy.Id == id);
    }

    [Fact]
    public Task OnNavigatedToAsync_LoadsThePlannedYearIntoTheFields_WhenOneExists() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var viewModel = Create(env).ViewModel;

        await viewModel.OnNavigatedToAsync();

        Assert.True(viewModel.IsNextYearCreated);
        Assert.Equal("2026/27", viewModel.NextYearLabel);
        Assert.Equal(NextStart, viewModel.NextYearStart);
        Assert.Equal(NextEnd, viewModel.NextYearEnd);
        Assert.False(viewModel.CreateNextYearCommand.CanExecute(null));
    });

    [Fact]
    public Task EditNextYearCommand_CannotExecute_WithoutAPlannedYear() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAsync(env);
        var viewModel = Create(env).ViewModel;

        await viewModel.OnNavigatedToAsync();

        Assert.False(viewModel.EditNextYearCommand.CanExecute(null));
        Assert.False(viewModel.ContinueToClassesCommand.CanExecute(null));
    });

    [Fact]
    public Task EditNextYearCommand_OpensTheNextYearStep_WithAPlannedYear() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var viewModel = Create(env).ViewModel;
        await viewModel.OnNavigatedToAsync();

        Assert.True(viewModel.EditNextYearCommand.CanExecute(null));
        viewModel.EditNextYearCommand.Execute(null);

        Assert.Equal(EndYearStep.NextYear, viewModel.CurrentStep);
        Assert.True(viewModel.IsNextYearStep);
    });

    [Fact]
    public Task SaveNextYearCommand_CannotExecute_WhenNothingChanged() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var viewModel = Create(env).ViewModel;

        await viewModel.OnNavigatedToAsync();

        Assert.False(viewModel.SaveNextYearCommand.CanExecute(null));
    });

    [Fact]
    public Task SaveNextYearCommand_CanExecute_AfterTheLabelChanged() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var viewModel = Create(env).ViewModel;
        await viewModel.OnNavigatedToAsync();

        viewModel.NextYearLabel = "Edited";

        Assert.True(viewModel.SaveNextYearCommand.CanExecute(null));
    });

    [Fact]
    public Task SaveNextYearCommand_CanExecute_AfterADateChanged() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var viewModel = Create(env).ViewModel;
        await viewModel.OnNavigatedToAsync();

        viewModel.NextYearStart = NextStart.AddDays(10);

        Assert.True(viewModel.SaveNextYearCommand.CanExecute(null));
    });

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public Task SaveNextYearCommand_CannotExecute_ForEmptyLabel(string label) => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var viewModel = Create(env).ViewModel;
        await viewModel.OnNavigatedToAsync();

        viewModel.NextYearLabel = label;

        Assert.False(viewModel.SaveNextYearCommand.CanExecute(null));
    });

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public Task SaveNextYearCommand_CannotExecute_WhenStartIsNotBeforeEnd(bool equal) => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var viewModel = Create(env).ViewModel;
        await viewModel.OnNavigatedToAsync();

        viewModel.NextYearStart = equal ? NextEnd : NextEnd.AddDays(1);

        Assert.False(viewModel.SaveNextYearCommand.CanExecute(null));
    });

    [Fact]
    public Task SaveNextYearCommand_SavesAndShowsSuccess_ForValidChanges() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var plannedId = await SeedActiveAndPlannedAsync(env);
        var viewModel = Create(env).ViewModel;
        await viewModel.OnNavigatedToAsync();
        viewModel.NextYearLabel = "  Edited  ";
        viewModel.NextYearStart = new DateTime(2026, 9, 1);

        await viewModel.SaveNextYearCommand.ExecuteAsync(null);

        var stored = await LoadAsync(env, plannedId);
        Assert.Equal("Edited", stored.Label);
        Assert.Equal(new DateTime(2026, 9, 1), stored.StartDate);
        Assert.Equal(NextEnd, stored.EndDate);
        Assert.Equal(["Success"], env.FakeSnackbar.Titles);
        Assert.Equal("Edited", viewModel.NextYearLabel);
        Assert.False(viewModel.SaveNextYearCommand.CanExecute(null));
        Assert.Equal(EndYearStep.Overview, viewModel.CurrentStep);
    });

    [Fact]
    public Task SaveNextYearCommand_ShowsErrorAndKeepsTypedValues_WhenTheDomainRejects() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var plannedId = await SeedActiveAndPlannedAsync(env);
        var viewModel = Create(env).ViewModel;
        await viewModel.OnNavigatedToAsync();
        viewModel.NextYearLabel = "2025/26";
        viewModel.NextYearStart = new DateTime(2026, 9, 1);

        await viewModel.SaveNextYearCommand.ExecuteAsync(null);

        Assert.Equal(["Error"], env.FakeSnackbar.Titles);
        Assert.Equal("2025/26", viewModel.NextYearLabel);
        Assert.Equal(new DateTime(2026, 9, 1), viewModel.NextYearStart);
        var stored = await LoadAsync(env, plannedId);
        Assert.Equal("2026/27", stored.Label);
        Assert.Equal(NextStart, stored.StartDate);
        Assert.True(viewModel.SaveNextYearCommand.CanExecute(null));
    });

    [Fact]
    public Task CreateNextYearCommand_CannotExecute_OnceTheNextYearExists() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAsync(env);
        var viewModel = Create(env).ViewModel;
        await viewModel.OnNavigatedToAsync();
        await viewModel.StartCommand.ExecuteAsync(null);
        Assert.True(viewModel.CreateNextYearCommand.CanExecute(null));

        await viewModel.CreateNextYearCommand.ExecuteAsync(null);

        Assert.True(viewModel.IsNextYearCreated);
        Assert.False(viewModel.CreateNextYearCommand.CanExecute(null));
        Assert.Equal(EndYearStep.Classes, viewModel.CurrentStep);
    });

    [Fact]
    public Task CanLeaveAsync_AllowsWithoutPrompt_WhenNothingChanged() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var (viewModel, dialog, _) = Create(env);
        await viewModel.OnNavigatedToAsync();

        var canLeave = await viewModel.CanLeaveAsync();

        Assert.True(canLeave);
        Assert.Equal(0, dialog.Shown);
    });

    [Fact]
    public Task CanLeaveAsync_AllowsWithoutPrompt_ForAnUnsavedDraftBeforeTheYearIsCreated() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAsync(env);
        var (viewModel, dialog, _) = Create(env);
        await viewModel.OnNavigatedToAsync();
        await viewModel.StartCommand.ExecuteAsync(null);
        viewModel.NextYearLabel = "Typed but not created";

        var canLeave = await viewModel.CanLeaveAsync();

        Assert.True(canLeave);
        Assert.Equal(0, dialog.Shown);
        Assert.Equal("Typed but not created", viewModel.NextYearLabel);
    });

    [Fact]
    public Task CanLeaveAsync_PromptsAndStaysWithInput_WhenTheUserKeepsEditing() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var (viewModel, dialog, _) = Create(env, ContentDialogResult.None);
        await viewModel.OnNavigatedToAsync();
        viewModel.NextYearLabel = "Edited";

        var canLeave = await viewModel.CanLeaveAsync();

        Assert.False(canLeave);
        Assert.Equal(1, dialog.Shown);
        Assert.Equal("Edited", viewModel.NextYearLabel);
    });

    [Fact]
    public Task CanLeaveAsync_PromptsAndRestoresSavedValues_WhenTheUserDiscards() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var (viewModel, dialog, _) = Create(env, ContentDialogResult.Primary);
        await viewModel.OnNavigatedToAsync();
        viewModel.NextYearLabel = "Edited";
        viewModel.NextYearStart = new DateTime(2026, 9, 1);
        viewModel.NextYearEnd = new DateTime(2027, 6, 30);

        var canLeave = await viewModel.CanLeaveAsync();

        Assert.True(canLeave);
        Assert.Equal(1, dialog.Shown);
        Assert.Equal("2026/27", viewModel.NextYearLabel);
        Assert.Equal(NextStart, viewModel.NextYearStart);
        Assert.Equal(NextEnd, viewModel.NextYearEnd);
        Assert.False(viewModel.SaveNextYearCommand.CanExecute(null));
    });

    [Fact]
    public Task BackCommand_PromptsAndStays_WhenEditsAreUnsavedAndTheUserKeepsEditing() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var (viewModel, dialog, _) = Create(env, ContentDialogResult.None);
        await viewModel.OnNavigatedToAsync();
        viewModel.EditNextYearCommand.Execute(null);
        viewModel.NextYearLabel = "Edited";

        await viewModel.BackCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.Shown);
        Assert.Equal(EndYearStep.NextYear, viewModel.CurrentStep);
        Assert.Equal("Edited", viewModel.NextYearLabel);
    });

    [Fact]
    public Task BackCommand_PromptsAndGoesBackRestoringValues_WhenTheUserDiscards() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var (viewModel, dialog, _) = Create(env, ContentDialogResult.Primary);
        await viewModel.OnNavigatedToAsync();
        viewModel.EditNextYearCommand.Execute(null);
        viewModel.NextYearLabel = "Edited";

        await viewModel.BackCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.Shown);
        Assert.Equal(EndYearStep.Overview, viewModel.CurrentStep);
        Assert.Equal("2026/27", viewModel.NextYearLabel);
    });

    [Fact]
    public Task BackCommand_GoesBackWithoutPrompt_WhenNothingChanged() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var (viewModel, dialog, _) = Create(env);
        await viewModel.OnNavigatedToAsync();
        viewModel.EditNextYearCommand.Execute(null);

        await viewModel.BackCommand.ExecuteAsync(null);

        Assert.Equal(0, dialog.Shown);
        Assert.Equal(EndYearStep.Overview, viewModel.CurrentStep);
    });

    [Fact]
    public Task ContinueToClassesCommand_PromptsAndStays_WhenEditsAreUnsavedAndTheUserKeepsEditing() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var (viewModel, dialog, _) = Create(env, ContentDialogResult.None);
        await viewModel.OnNavigatedToAsync();
        viewModel.EditNextYearCommand.Execute(null);
        viewModel.NextYearLabel = "Edited";

        await viewModel.ContinueToClassesCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.Shown);
        Assert.Equal(EndYearStep.NextYear, viewModel.CurrentStep);
        Assert.Equal("Edited", viewModel.NextYearLabel);
    });

    [Fact]
    public Task ContinueToClassesCommand_ContinuesAndRestoresValues_WhenTheUserDiscards() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var (viewModel, dialog, _) = Create(env, ContentDialogResult.Primary);
        await viewModel.OnNavigatedToAsync();
        viewModel.EditNextYearCommand.Execute(null);
        viewModel.NextYearLabel = "Edited";

        await viewModel.ContinueToClassesCommand.ExecuteAsync(null);

        Assert.Equal(1, dialog.Shown);
        Assert.Equal(EndYearStep.Classes, viewModel.CurrentStep);
        Assert.Equal("2026/27", viewModel.NextYearLabel);
    });

    [Fact]
    public Task ContinueToClassesCommand_ContinuesWithoutPrompt_WhenNothingChanged() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedActiveAndPlannedAsync(env);
        var (viewModel, dialog, _) = Create(env);
        await viewModel.OnNavigatedToAsync();
        viewModel.EditNextYearCommand.Execute(null);

        await viewModel.ContinueToClassesCommand.ExecuteAsync(null);

        Assert.Equal(0, dialog.Shown);
        Assert.Equal(EndYearStep.Classes, viewModel.CurrentStep);
    });

    [Fact]
    public Task NextYearValidationMessage_AsksForALabel_WhenTheLabelIsEmpty() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = Create(env).ViewModel;
        await Task.CompletedTask;

        viewModel.NextYearLabel = "  ";
        viewModel.NextYearStart = NextStart;
        viewModel.NextYearEnd = NextEnd;

        Assert.Equal(TextResources.Text("SchoolYears_NextYear_Validation_LabelRequired"), viewModel.NextYearValidationMessage);
        Assert.True(viewModel.HasNextYearValidationMessage);
    });

    [Fact]
    public Task NextYearValidationMessage_AsksForDates_WhenADateIsMissing() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = Create(env).ViewModel;
        await Task.CompletedTask;

        viewModel.NextYearLabel = "2026/27";
        viewModel.NextYearStart = NextStart;
        viewModel.NextYearEnd = null;

        Assert.Equal(TextResources.Text("SchoolYears_NextYear_Validation_DatesRequired"), viewModel.NextYearValidationMessage);
        Assert.True(viewModel.HasNextYearValidationMessage);
    });

    [Fact]
    public Task NextYearValidationMessage_ExplainsTheOrder_WhenStartIsNotBeforeEnd() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = Create(env).ViewModel;
        await Task.CompletedTask;

        viewModel.NextYearLabel = "2026/27";
        viewModel.NextYearStart = NextEnd;
        viewModel.NextYearEnd = NextEnd;

        Assert.Equal(TextResources.Text("SchoolYears_NextYear_Validation_DateOrder"), viewModel.NextYearValidationMessage);
        Assert.True(viewModel.HasNextYearValidationMessage);
    });

    [Fact]
    public Task NextYearValidationMessage_IsNull_ForAValidLabelAndRange() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = Create(env).ViewModel;
        await Task.CompletedTask;

        viewModel.NextYearLabel = "2026/27";
        viewModel.NextYearStart = NextStart;
        viewModel.NextYearEnd = NextEnd;

        Assert.Null(viewModel.NextYearValidationMessage);
        Assert.False(viewModel.HasNextYearValidationMessage);
    });
}
