using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core;
using NexGrades.App.Features.Classes;
using NexGrades.App.Tests.TestSupport;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests.Features.Classes;

public class ClassesOverviewViewModelTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0);

    private static SchoolYearService YearService(LockTestEnvironment env, IDbContextFactory<AppDbContext> factory) =>
        new(factory, env.Clock, NullLogger<SchoolYearService>.Instance);

    private static ClassesOverviewViewModel Create(LockTestEnvironment env)
    {
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        return new ClassesOverviewViewModel(navigator, env.Factory, YearService(env, env.Factory), env.Snackbar, NullLogger<ClassesOverviewViewModel>.Instance);
    }

    private static async Task<int> AddYearAsync(AppDbContext context, string label, int startYear, SchoolYearStatus status)
    {
        var year = new SchoolYear
        {
            Label = label,
            StartDate = new DateTime(startYear, 8, 1),
            EndDate = new DateTime(startYear + 1, 7, 31),
            Status = status,
        };
        context.SchoolYears.Add(year);
        await context.SaveChangesAsync();
        return year.Id;
    }

    private static ClassInstance Instance(int foundedIn, int yearId, string name, ClassInstanceStatus status) =>
        new() { ClassGroup = new ClassGroup { CohortLabel = "Cohort", FoundedInSchoolYearId = foundedIn }, SchoolYearId = yearId, Name = name, Status = status };

    /// <summary>Seeds a closed year with one active class and an active year with one class per status.</summary>
    private static async Task<Dictionary<string, int>> SeedAsync(LockTestEnvironment env)
    {
        await using var context = new AppDbContext(env.Options);
        var closedId = await AddYearAsync(context, "2024/25", 2024, SchoolYearStatus.Closed);
        var activeId = await AddYearAsync(context, "2025/26", 2025, SchoolYearStatus.Active);
        var rows = new[]
        {
            Instance(closedId, closedId, "Old", ClassInstanceStatus.Active),
            Instance(closedId, closedId, "Old-Archived", ClassInstanceStatus.Archived),
            Instance(closedId, activeId, "A-Active", ClassInstanceStatus.Active),
            Instance(closedId, activeId, "B-Rolled", ClassInstanceStatus.RolledForward),
            Instance(closedId, activeId, "C-Archived", ClassInstanceStatus.Archived),
        };
        context.ClassInstances.AddRange(rows);
        await context.SaveChangesAsync();
        return rows.ToDictionary(r => r.Name, r => r.Id);
    }

    private static string[] Names(ClassesOverviewViewModel vm) => [.. vm.Classes.Select(c => c.Name)];

    [Fact]
    public Task LoadClassesCommand_ListsOnlyActiveInstancesOfTheActiveYear_ByDefault() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.False(vm.ShowInactiveClasses);
        Assert.Equal(["A-Active"], Names(vm));
    });

    [Fact]
    public Task ShowInactiveClasses_ListsRolledForwardAndArchivedWithBadges_WhenTurnedOn() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);
        await vm.LoadClassesCommand.ExecuteAsync(null);

        vm.ShowInactiveClasses = true;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 3);

        Assert.Equal(["A-Active", "B-Rolled", "C-Archived"], Names(vm));
        Assert.False(vm.Classes[0].HasStatusBadge);
        Assert.True(vm.Classes[1].HasStatusBadge);
        Assert.True(vm.Classes[2].HasStatusBadge);
    });

    [Fact]
    public Task ShowInactiveClasses_HidesInactiveClassesAgain_WhenTurnedOff() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);
        vm.ShowInactiveClasses = true;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 3);

        vm.ShowInactiveClasses = false;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 1);

        Assert.Equal(["A-Active"], Names(vm));
    });

    [Fact]
    public Task ShowInactiveClasses_NeverListsInstancesOfOtherYears_WhenTurnedOn() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);

        vm.ShowInactiveClasses = true;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 3);

        Assert.DoesNotContain("Old", Names(vm));
    });

    [Fact]
    public Task LoadClassesCommand_KeepsTheSelection_WhenTheSelectedClassIsStillListed() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var ids = await SeedAsync(env);
        var vm = Create(env);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        vm.SelectedClass = vm.Classes.Single();

        vm.ShowInactiveClasses = true;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 3);

        Assert.NotNull(vm.SelectedClass);
        Assert.Equal(ids["A-Active"], vm.SelectedClass.Id);
        Assert.Contains(vm.SelectedClass, vm.Classes);
    });

    [Fact]
    public Task LoadClassesCommand_ClearsTheSelection_WhenTheSelectedClassIsNoLongerListed() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);
        vm.ShowInactiveClasses = true;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 3);
        vm.SelectedClass = vm.Classes.Single(c => c.Name == "C-Archived");

        vm.ShowInactiveClasses = false;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 1);

        Assert.Null(vm.SelectedClass);
    });

    private static SchoolYearSummary YearNamed(ClassesOverviewViewModel vm, string label) => vm.Years.Single(y => y.Label == label);

    private static async Task AddPlannedYearAsync(LockTestEnvironment env)
    {
        await using var context = new AppDbContext(env.Options);
        await AddYearAsync(context, "2026/27", 2026, SchoolYearStatus.Planned);
    }

    [Fact]
    public Task LoadClassesCommand_ListsYearsNewestFirst_OnFirstLoad() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        await AddPlannedYearAsync(env);
        var vm = Create(env);

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.Equal(["2026/27", "2025/26", "2024/25"], vm.Years.Select(y => y.Label));
    });

    [Fact]
    public Task LoadClassesCommand_SelectsTheActiveYear_OnFirstLoad() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        await AddPlannedYearAsync(env);
        var vm = Create(env);

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.Equal("2025/26", vm.SelectedYear?.Label);
    });

    [Fact]
    public Task LoadClassesCommand_SelectsTheNewestYear_WhenNoYearIsActive() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await using (var context = new AppDbContext(env.Options))
        {
            var older = await AddYearAsync(context, "2023/24", 2023, SchoolYearStatus.Closed);
            var newer = await AddYearAsync(context, "2024/25", 2024, SchoolYearStatus.Closed);
            context.ClassInstances.AddRange(
                Instance(older, older, "Older", ClassInstanceStatus.Active),
                Instance(older, newer, "Newer", ClassInstanceStatus.Active));
            await context.SaveChangesAsync();
        }

        var vm = Create(env);

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.Equal("2024/25", vm.SelectedYear?.Label);
        Assert.Equal(["Newer"], Names(vm));
    });

    [Fact]
    public Task LoadClassesCommand_ListsNoClassesAndNoYear_WhenThereAreNoYears() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var vm = Create(env);

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.Null(vm.SelectedYear);
        Assert.Empty(vm.Years);
        Assert.Empty(vm.Classes);
    });

    [Fact]
    public Task SelectedYear_ListsActiveClassesOfThatYear_WhenAClosedYearIsChosen() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);
        await vm.LoadClassesCommand.ExecuteAsync(null);

        vm.SelectedYear = YearNamed(vm, "2024/25");
        await LockTestEnvironment.WaitUntilAsync(() => Names(vm) is ["Old"]);

        Assert.Equal("2024/25", vm.SelectedYear.Label);
    });

    [Fact]
    public Task ShowInactiveClasses_ListsArchivedClassWithBadgeOfAClosedYear_WhenTurnedOn() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        vm.SelectedYear = YearNamed(vm, "2024/25");
        await LockTestEnvironment.WaitUntilAsync(() => Names(vm) is ["Old"]);

        vm.ShowInactiveClasses = true;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 2);

        Assert.Equal(["Old", "Old-Archived"], Names(vm));
        Assert.False(vm.Classes[0].HasStatusBadge);
        Assert.True(vm.Classes[1].HasStatusBadge);
    });

    [Fact]
    public Task SelectedYear_ListsNoClasses_WhenAPlannedYearHasNone() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        await AddPlannedYearAsync(env);
        var vm = Create(env);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        Assert.NotEmpty(vm.Classes);

        vm.SelectedYear = YearNamed(vm, "2026/27");
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 0);

        Assert.Equal("2026/27", vm.SelectedYear.Label);
    });

    [Fact]
    public Task SelectedYear_SurvivesAToggleFlip_WhenAnotherYearIsChosen() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        vm.SelectedYear = YearNamed(vm, "2024/25");
        await LockTestEnvironment.WaitUntilAsync(() => Names(vm) is ["Old"]);

        vm.ShowInactiveClasses = true;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 2);
        vm.ShowInactiveClasses = false;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 1);

        Assert.Equal("2024/25", vm.SelectedYear?.Label);
        Assert.Equal(["Old"], Names(vm));
    });

    [Fact]
    public Task LoadClassesCommand_KeepsTheSelectedYear_WhenReloaded() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        vm.SelectedYear = YearNamed(vm, "2024/25");
        await LockTestEnvironment.WaitUntilAsync(() => Names(vm) is ["Old"]);

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.Equal("2024/25", vm.SelectedYear?.Label);
        Assert.Equal(["Old"], Names(vm));
    });

    [Fact]
    public Task LoadClassesCommand_FallsBackToTheActiveYear_WhenTheSelectedYearNoLongerExists() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        var missing = new SchoolYearSummary(9999, "2020/21", new DateTime(2020, 8, 1), new DateTime(2021, 7, 31), SchoolYearStatus.Closed);

        vm.SelectedYear = missing;
        await LockTestEnvironment.WaitUntilAsync(() => vm.SelectedYear?.Label == "2025/26");

        Assert.Equal(SchoolYearStatus.Active, vm.SelectedYear.Status);
        Assert.DoesNotContain(vm.Years, y => y.Id == missing.Id);
        Assert.Equal(["A-Active"], Names(vm));
    });

    [Fact]
    public Task SelectedYear_ClearsSelectedClass_WhenTheClassIsNotInTheNewYear() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        vm.SelectedClass = vm.Classes.Single();

        vm.SelectedYear = YearNamed(vm, "2024/25");
        await LockTestEnvironment.WaitUntilAsync(() => Names(vm) is ["Old"]);

        Assert.Null(vm.SelectedClass);
    });

    [Fact]
    public Task LoadClassesCommand_KeepsTheNewestYearAndToggle_WhenAnOlderYearLoadIsHeldAndSuperseded() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var gate = new QueryGatedContextFactory(env.Options);
        var logger = new CapturingLogger<ClassesOverviewViewModel>();
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var vm = new ClassesOverviewViewModel(navigator, gate, YearService(env, gate), env.Snackbar, logger);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        var closed = YearNamed(vm, "2024/25");

        var held = gate.HoldNextQueryAsync(out var release);
        vm.SelectedYear = closed;
        await held;
        var older = vm.LoadClassesCommand.ExecutionTask!;
        vm.ShowInactiveClasses = true;
        await LockTestEnvironment.WaitUntilAsync(() => Names(vm) is ["Old", "Old-Archived"]);
        release();
        await older;

        Assert.Equal(["Old", "Old-Archived"], Names(vm));
        Assert.Equal("2024/25", vm.SelectedYear?.Label);
        Assert.True(vm.ShowInactiveClasses);
        Assert.Empty(logger.Entries);
    });

    [Fact]
    public void ClassRow_HasNoBadgeAndEmptyText_ForAnActiveClass()
    {
        var row = new ClassRow(1, "Name", "Cohort", 0, ClassInstanceStatus.Active);

        Assert.False(row.HasStatusBadge);
        Assert.Equal(string.Empty, row.StatusText);
    }

    [Theory]
    [InlineData(ClassInstanceStatus.RolledForward, "Rolled forward")]
    [InlineData(ClassInstanceStatus.Archived, "Archived")]
    public void ClassRow_HasBadgeAndLocalizedText_ForAnInactiveClass(ClassInstanceStatus status, string expected)
    {
        var row = new ClassRow(1, "Name", "Cohort", 0, status);

        Assert.True(row.HasStatusBadge);
        Assert.Equal(expected, row.StatusText);
    }

    /// <summary>
    /// Toggles the filter twice through the real command: the first (older) load is held after the database answered,
    /// the second cancels and supersedes it and finishes first, then the older load is released and awaited.
    /// </summary>
    private static async Task<(ClassesOverviewViewModel Vm, CapturingLogger<ClassesOverviewViewModel> Logger)> SupersedeHeldLoadAsync(LockTestEnvironment env)
    {
        await SeedAsync(env);
        var gate = new QueryGatedContextFactory(env.Options);
        var logger = new CapturingLogger<ClassesOverviewViewModel>();
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var vm = new ClassesOverviewViewModel(navigator, gate, YearService(env, gate), env.Snackbar, logger);

        var held = gate.HoldNextQueryAsync(out var release);
        vm.ShowInactiveClasses = true;
        await held;
        var older = vm.LoadClassesCommand.ExecutionTask!;
        vm.ShowInactiveClasses = false;
        await LockTestEnvironment.WaitUntilAsync(() => vm.Classes.Count == 1);
        release();
        await older;
        return (vm, logger);
    }

    [Fact]
    public Task LoadClassesCommand_KeepsTheNewestResult_WhenAnOlderLoadIsCancelledAndFinishesLast() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);

        var (vm, _) = await SupersedeHeldLoadAsync(env);

        Assert.Equal(["A-Active"], Names(vm));
    });

    [Fact]
    public Task LoadClassesCommand_LogsNoWarning_WhenARunIsCancelledBySupersedingOne() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);

        var (_, logger) = await SupersedeHeldLoadAsync(env);

        Assert.Empty(logger.Entries);
    });

    [Fact]
    public Task LoadClassesCommand_LogsOneWarningWithoutRecordContentAndKeepsTheList_WhenTheReadFails() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var flaky = new FlakyContextFactory(env.Factory, 2);
        var logger = new CapturingLogger<ClassesOverviewViewModel>();
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var vm = new ClassesOverviewViewModel(navigator, flaky, YearService(env, flaky), env.Snackbar, logger);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        var listed = vm.Classes;

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.Same(listed, vm.Classes);
        Assert.Equal(["A-Active"], Names(vm));
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
        foreach (var content in new[] { "A-Active", "B-Rolled", "C-Archived", "Old", "2024/25", "2025/26" })
        {
            Assert.DoesNotContain(content, entry.Message, StringComparison.Ordinal);
        }
    });

    [Fact]
    public void HasLoaded_IsFalse_BeforeAnyLoad()
    {
        using var env = new LockTestEnvironment(Now);

        var vm = Create(env);

        Assert.False(vm.HasLoaded);
    }

    [Fact]
    public Task HasLoaded_IsTrue_AfterTheFirstLoadWithClasses() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var vm = Create(env);

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.True(vm.HasLoaded);
    });

    [Fact]
    public Task HasLoaded_IsTrue_AfterTheFirstLoadWithNoYears() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        var vm = Create(env);

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.True(vm.HasLoaded);
        Assert.Empty(vm.Classes);
    });

    [Fact]
    public Task HasLoaded_IsTrue_AfterAFailedFirstLoad() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var flaky = new FlakyContextFactory(env.Factory, 0);
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var vm = new ClassesOverviewViewModel(navigator, flaky, YearService(env, flaky), env.Snackbar, new CapturingLogger<ClassesOverviewViewModel>());

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.True(vm.HasLoaded);
        Assert.Empty(vm.Classes);
    });

    [Fact]
    public Task HasLoaded_IsFalse_WhileTheOnlyLoadIsHeld() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var gate = new QueryGatedContextFactory(env.Options);
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var vm = new ClassesOverviewViewModel(navigator, gate, YearService(env, gate), env.Snackbar, NullLogger<ClassesOverviewViewModel>.Instance);

        var held = gate.HoldNextQueryAsync(out var release);
        var load = vm.LoadClassesCommand.ExecuteAsync(null);
        await held;
        var beforeRelease = vm.HasLoaded;
        release();
        await load;

        Assert.False(beforeRelease);
        Assert.True(vm.HasLoaded);
    });

    /// <summary>
    /// Starts a load outside the command, with a token that is never cancelled. Through the command, a newer run cancels
    /// the older one, whose gated class read then throws <see cref="OperationCanceledException"/> before the
    /// version guard is reached; only an uncancelled older run can reach it.
    /// </summary>
    private static Task StartUncancelledLoadAsync(ClassesOverviewViewModel vm) =>
        (Task)typeof(ClassesOverviewViewModel)
            .GetMethod("LoadClassesAsync", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
            .Invoke(vm, [CancellationToken.None])!;

    [Fact]
    public Task LoadClassesCommand_ChangesNothing_WhenASupersededLoadFinishesAfterTheNewestOne() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var gate = new QueryGatedContextFactory(env.Options);
        // Only the class query is held (the years read uses the ungated factory), so the superseded run reaches the version guard.
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var vm = new ClassesOverviewViewModel(navigator, gate, YearService(env, env.Factory), env.Snackbar, NullLogger<ClassesOverviewViewModel>.Instance);

        var held = gate.HoldNextQueryAsync(out var release);
        var older = StartUncancelledLoadAsync(vm);
        await held;
        vm.ShowInactiveClasses = true;
        await LockTestEnvironment.WaitUntilAsync(() => vm.HasLoaded && Names(vm) is ["A-Active", "B-Rolled", "C-Archived"]);
        var listed = vm.Classes;
        var year = vm.SelectedYear;
        release();
        await older;

        Assert.True(vm.HasLoaded);
        Assert.Same(listed, vm.Classes);
        Assert.Equal(["A-Active", "B-Rolled", "C-Archived"], Names(vm));
        Assert.Same(year, vm.SelectedYear);
    });

    [Fact]
    public Task HasLoaded_StaysFalse_WhenASupersededLoadFinishesWhileTheNewestIsHeld() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var gate = new QueryGatedContextFactory(env.Options);
        // Only the class query is held (the years read uses the ungated factory), so the superseded run reaches the version guard.
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var vm = new ClassesOverviewViewModel(navigator, gate, YearService(env, env.Factory), env.Snackbar, NullLogger<ClassesOverviewViewModel>.Instance);

        var heldOlder = gate.HoldNextQueryAsync(out var releaseOlder);
        var older = StartUncancelledLoadAsync(vm);
        await heldOlder;
        var heldNewer = gate.HoldNextQueryAsync(out var releaseNewer);
        vm.ShowInactiveClasses = true;
        await heldNewer;
        var newer = vm.LoadClassesCommand.ExecutionTask!;
        releaseOlder();
        await older;
        var whileNewerHeld = vm.HasLoaded;
        releaseNewer();
        await newer;

        Assert.False(whileNewerHeld);
        Assert.True(vm.HasLoaded);
        Assert.Equal(["A-Active", "B-Rolled", "C-Archived"], Names(vm));
    });
}
