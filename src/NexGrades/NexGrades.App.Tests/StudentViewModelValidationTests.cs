using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.App.Core.YearLock;
using NexGrades.App.Features.Students;
using NexGrades.Domain.Services;

namespace NexGrades.App.Tests;

public class StudentViewModelValidationTests
{
    private static readonly DateTime Now = new(2025, 10, 1);

    // Only the name properties and the save command's availability are exercised, and neither touches the
    // collaborators other than the year lock the constructor creates, so the rest are passed as null.
    private static StudentViewModel Create(LockTestEnvironment env) => new(
        null!, env.Snackbar, null!, null!, null!, null!,
        new YearLockViewModelFactory(env.Monitor, new FakeUnlockDialog(), env.Snackbar, env.Clock, NullLogger<YearLockViewModel>.Instance),
        null!, null!, null!, null!,
        NullLogger<StudentViewModel>.Instance);

    private static StudentViewModel CreateExisting(LockTestEnvironment env, string firstName, string lastName)
    {
        var viewModel = Create(env);
        viewModel.IsExistingStudent = true;
        viewModel.FirstName = firstName;
        viewModel.LastName = lastName;
        return viewModel;
    }

    [Fact]
    public void MaxNameLength_MatchesTheService()
    {
        using var env = new LockTestEnvironment(Now);

        Assert.Equal(StudentService.MaxNameLength, Create(env).MaxNameLength);
    }

    [Fact]
    public void StudentValidationMessage_IsNull_ForNamesAtTheLimit()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = CreateExisting(env, new string('a', StudentService.MaxNameLength), new string('b', StudentService.MaxNameLength));

        Assert.Null(viewModel.StudentValidationMessage);
        Assert.True(viewModel.SaveStudentCommand.CanExecute(null));
    }

    [Fact]
    public void StudentValidationMessage_IsNull_WhenOnlyTrailingWhitespacePushesFirstNamePastTheLimit()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = CreateExisting(env, new string('a', StudentService.MaxNameLength) + "  ", "Beta");

        Assert.Null(viewModel.StudentValidationMessage);
    }

    [Fact]
    public void StudentValidationMessage_IsSetAndSaveDisabled_WhenFirstNameIsTooLong()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = CreateExisting(env, new string('a', StudentService.MaxNameLength + 1), "Beta");

        Assert.NotNull(viewModel.StudentValidationMessage);
        Assert.True(viewModel.HasStudentValidationMessage);
        Assert.False(viewModel.SaveStudentCommand.CanExecute(null));
    }

    [Fact]
    public void StudentValidationMessage_IsSetAndSaveDisabled_WhenLastNameIsTooLong()
    {
        using var env = new LockTestEnvironment(Now);
        var viewModel = CreateExisting(env, "Alpha", new string('b', StudentService.MaxNameLength + 1));

        Assert.NotNull(viewModel.StudentValidationMessage);
        Assert.False(viewModel.SaveStudentCommand.CanExecute(null));
    }
}
