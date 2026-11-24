using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core;
using NexGrades.App.Features.Classes;
using NexGrades.App.Tests.TestSupport;
using NexGrades.Data;
using NexGrades.Data.Entities;

namespace NexGrades.App.Tests.Features.Classes;

public class ClassesOverviewViewModelTests
{
    private static readonly DateTime Now = new(2026, 3, 1, 12, 0, 0);

    private static ClassesOverviewViewModel Create(LockTestEnvironment env)
    {
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        return new ClassesOverviewViewModel(navigator, env.Factory, env.Snackbar, NullLogger<ClassesOverviewViewModel>.Instance);
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
        var vm = new ClassesOverviewViewModel(navigator, gate, env.Snackbar, logger);

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
    public Task LoadClassesCommand_LogsOneWarningAndKeepsTheList_WhenTheReadFails() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(Now);
        await SeedAsync(env);
        var flaky = new FlakyContextFactory(env.Factory, 1);
        var logger = new CapturingLogger<ClassesOverviewViewModel>();
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        var vm = new ClassesOverviewViewModel(navigator, flaky, env.Snackbar, logger);
        await vm.LoadClassesCommand.ExecuteAsync(null);
        var listed = vm.Classes;

        await vm.LoadClassesCommand.ExecuteAsync(null);

        Assert.Same(listed, vm.Classes);
        Assert.Equal(["A-Active"], Names(vm));
        var entry = Assert.Single(logger.Entries);
        Assert.Equal(Microsoft.Extensions.Logging.LogLevel.Warning, entry.Level);
        Assert.IsType<InvalidOperationException>(entry.Exception);
    });
}
