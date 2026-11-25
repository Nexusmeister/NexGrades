using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core;
using NexGrades.App.Features.Students;
using NexGrades.App.Tests.TestSupport;
using NexGrades.Data;
using NexGrades.Data.Entities;

namespace NexGrades.App.Tests.Features.Students;

public class StudentsOverviewViewModelTests
{
    private static StudentsOverviewViewModel Create(LockTestEnvironment env, IDbContextFactory<AppDbContext>? factory = null)
    {
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        return new StudentsOverviewViewModel(navigator, factory ?? env.Factory, env.Snackbar);
    }

    [Fact]
    public void HasLoaded_IsFalse_BeforeAnyLoad()
    {
        using var env = new LockTestEnvironment(new DateTime(2026, 3, 1));

        var vm = Create(env);

        Assert.False(vm.HasLoaded);
    }

    [Fact]
    public Task HasLoaded_IsTrue_AfterLoadingNoStudents() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2026, 3, 1));
        var vm = Create(env);

        await vm.LoadStudentsCommand.ExecuteAsync(null);

        Assert.True(vm.HasLoaded);
        Assert.Empty(vm.Students);
    });

    [Fact]
    public Task HasLoaded_IsTrue_AfterLoadingStudents() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2026, 3, 1));
        await using (var context = new AppDbContext(env.Options))
        {
            context.Students.Add(new Student { FirstName = "Test", LastName = "Student" });
            await context.SaveChangesAsync();
        }

        var vm = Create(env);

        await vm.LoadStudentsCommand.ExecuteAsync(null);

        Assert.True(vm.HasLoaded);
        Assert.Single(vm.Students);
    });

    [Fact]
    public Task HasLoaded_IsTrue_AndTheErrorIsRethrown_WhenTheLoadFails() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2026, 3, 1));
        var vm = Create(env, new FlakyContextFactory(env.Factory, 0));

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadStudentsCommand.ExecuteAsync(null));

        Assert.True(vm.HasLoaded);
    });
}
