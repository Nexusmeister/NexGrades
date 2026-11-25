using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core;
using NexGrades.App.Features.Subjects;
using NexGrades.App.Tests.TestSupport;
using NexGrades.Data;
using NexGrades.Data.Entities;

namespace NexGrades.App.Tests.Features.Subjects;

public class SubjectsOverviewViewModelTests
{
    private static SubjectsOverviewViewModel Create(IDbContextFactory<AppDbContext> factory)
    {
        var navigator = new PageNavigator(FakeNavigationService.Create(), NullLogger<PageNavigator>.Instance, _ => { });
        return new SubjectsOverviewViewModel(navigator, factory);
    }

    [Fact]
    public void HasLoaded_IsFalse_BeforeAnyLoad()
    {
        using var env = new LockTestEnvironment(new DateTime(2026, 3, 1));

        var vm = Create(env.Factory);

        Assert.False(vm.HasLoaded);
    }

    [Fact]
    public Task HasLoaded_IsTrue_AfterLoadingNoSubjects() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2026, 3, 1));
        var vm = Create(env.Factory);

        await vm.LoadSubjectsCommand.ExecuteAsync(null);

        Assert.True(vm.HasLoaded);
        Assert.Empty(vm.Subjects);
    });

    [Fact]
    public Task HasLoaded_IsTrue_AfterLoadingSubjects() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2026, 3, 1));
        await using (var context = new AppDbContext(env.Options))
        {
            context.Subjects.Add(new Subject { Name = "Test subject", ShortCode = "Ts" });
            await context.SaveChangesAsync();
        }

        var vm = Create(env.Factory);

        await vm.LoadSubjectsCommand.ExecuteAsync(null);

        Assert.True(vm.HasLoaded);
        Assert.Single(vm.Subjects);
    });

    [Fact]
    public Task HasLoaded_IsTrue_AndTheErrorIsRethrown_WhenTheLoadFails() => StaTestRunner.RunAsync(async () =>
    {
        using var env = new LockTestEnvironment(new DateTime(2026, 3, 1));
        var vm = Create(new FlakyContextFactory(env.Factory, 0));

        await Assert.ThrowsAsync<InvalidOperationException>(() => vm.LoadSubjectsCommand.ExecuteAsync(null));

        Assert.True(vm.HasLoaded);
    });
}
