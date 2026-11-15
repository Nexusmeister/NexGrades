using NexGrades.App.Features.Classes;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class ClassViewModelValidationTests
{
    private static readonly DateTime Now = new(2025, 10, 1);

    private static readonly string LegacyName = new('L', ClassService.MaxNameLength + 10);

    /// <summary>Opens the protected navigation entry point so a test can load an existing class.</summary>
    private sealed class OpenableClassViewModel(LockTestEnvironment env) : ClassViewModel(
        null!, env.Snackbar, env.Factory, null!, null!, null!, null!, null!)
    {
        public Task OpenAsync(int classInstanceId) => OnNavigatedToAsync((object?)classInstanceId);

        public Task OpenNewAsync() => OnNavigatedToAsync((object?)null);
    }

    // Only the name, the editing flag and the save command's availability are exercised, and none of them touch the
    // collaborators the form uses for saving and exporting, so those are passed as null.
    private static ClassViewModel Create(LockTestEnvironment env) => new(
        null!, env.Snackbar, null!, null!, null!, null!, null!, null!);

    private static async Task<int> SeedClassAsync(LockTestEnvironment env, string name)
    {
        await using var context = new AppDbContext(env.Options);
        var year = new SchoolYear
        {
            Label = "2025/26",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = SchoolYearStatus.Active,
        };
        var group = new ClassGroup { CohortLabel = "Test cohort", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = name };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();
        return instance.Id;
    }

    private static async Task<OpenableClassViewModel> OpenExistingAsync(LockTestEnvironment env, string name)
    {
        var id = await SeedClassAsync(env, name);
        var viewModel = new OpenableClassViewModel(env);
        await viewModel.OpenAsync(id);
        return viewModel;
    }

    [Fact]
    public void MaxNameLength_MatchesTheService()
    {
        using var env = new LockTestEnvironment(Now);

        Assert.Equal(ClassService.MaxNameLength, Create(env).MaxNameLength);
    }

    [Fact]
    public void ClassValidationMessage_IsNull_ForNameAtTheLimit()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = Create(env);

        viewModel.Name = new string('a', ClassService.MaxNameLength);

        Assert.Null(viewModel.ClassValidationMessage);
        Assert.False(viewModel.HasClassValidationMessage);
        Assert.True(viewModel.SaveClassCommand.CanExecute(null));
    }

    [Fact]
    public void ClassValidationMessage_IsSetAndSaveDisabled_ForNameOneOverTheLimit()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = Create(env);

        viewModel.Name = new string('a', ClassService.MaxNameLength + 1);

        Assert.NotNull(viewModel.ClassValidationMessage);
        Assert.True(viewModel.HasClassValidationMessage);
        Assert.False(viewModel.SaveClassCommand.CanExecute(null));
    }

    [Fact]
    public void ClassValidationMessage_IsNull_WhenOnlyPaddingPushesTheNamePastTheLimit()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = Create(env);

        viewModel.Name = "  " + new string('a', ClassService.MaxNameLength) + "  ";

        Assert.Null(viewModel.ClassValidationMessage);
        Assert.True(viewModel.SaveClassCommand.CanExecute(null));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ClassValidationMessage_IsSetAndSaveDisabled_ForEmptyName(string name)
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = Create(env);

        viewModel.Name = name;

        Assert.NotNull(viewModel.ClassValidationMessage);
        Assert.False(viewModel.SaveClassCommand.CanExecute(null));
    }

    [Fact]
    public void ClassValidationMessage_RaisesPropertyChanged_WhenTheNameChanges()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = Create(env);
        var raised = new List<string?>();
        viewModel.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        viewModel.Name = "5A";

        Assert.Contains(nameof(ClassViewModel.ClassValidationMessage), raised);
        Assert.Contains(nameof(ClassViewModel.HasClassValidationMessage), raised);
    }

    [Fact]
    public async Task ClassValidationMessage_IsNull_ForUnchangedLegacyLongNameInEditMode()
    {
        using var env = new LockTestEnvironment(Now);

        var viewModel = await OpenExistingAsync(env, LegacyName);

        Assert.True(viewModel.IsEditing);
        Assert.Null(viewModel.ClassValidationMessage);
        Assert.True(viewModel.SaveClassCommand.CanExecute(null));
    }

    [Fact]
    public async Task ClassValidationMessage_IsNull_ForUnchangedLegacyLongNameWithPadding()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = await OpenExistingAsync(env, LegacyName);

        viewModel.Name = "  " + LegacyName + " ";

        Assert.Null(viewModel.ClassValidationMessage);
    }

    [Fact]
    public async Task ClassValidationMessage_IsSetAndSaveDisabled_ForChangedLongNameInEditMode()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = await OpenExistingAsync(env, LegacyName);

        viewModel.Name = LegacyName + "x";

        Assert.NotNull(viewModel.ClassValidationMessage);
        Assert.False(viewModel.SaveClassCommand.CanExecute(null));
    }

    [Fact]
    public async Task ClassValidationMessage_IsNull_ForShortNameInEditModeOfLegacyLongClass()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = await OpenExistingAsync(env, LegacyName);

        viewModel.Name = "5A";

        Assert.Null(viewModel.ClassValidationMessage);
        Assert.True(viewModel.SaveClassCommand.CanExecute(null));
    }

    [Fact]
    public async Task ClassValidationMessage_IsSet_ForLegacyLongNameOnceBackInAddMode()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = await OpenExistingAsync(env, LegacyName);

        await viewModel.OpenNewAsync();
        viewModel.Name = LegacyName;

        Assert.False(viewModel.IsEditing);
        Assert.NotNull(viewModel.ClassValidationMessage);
    }
}
