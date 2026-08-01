using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class EnrollmentServiceTests
{
    [Fact]
    public async Task CreateEnrollmentAsync_Succeeds_ForAFirstEnrollmentInAYear()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var classInstance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A" };
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        context.AddRange(year, group, classInstance, student);
        await context.SaveChangesAsync();

        var service = new EnrollmentService(context);
        var enrollment = await service.CreateEnrollmentAsync(student.Id, classInstance.Id, year.StartDate);

        Assert.NotEqual(0, enrollment.Id);
        Assert.Equal(student.Id, enrollment.StudentId);
        Assert.Equal(classInstance.Id, enrollment.ClassInstanceId);
    }

    [Fact]
    public async Task CreateEnrollmentAsync_Throws_WhenStudentAlreadyEnrolledElsewhereInSameSchoolYear()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var groupA = new ClassGroup { CohortLabel = "Kohorte A", FoundedInSchoolYear = year };
        var groupB = new ClassGroup { CohortLabel = "Kohorte B", FoundedInSchoolYear = year };
        var classA = new ClassInstance { ClassGroup = groupA, SchoolYear = year, Name = "5A" };
        var classB = new ClassInstance { ClassGroup = groupB, SchoolYear = year, Name = "5B" };
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        context.AddRange(year, groupA, groupB, classA, classB, student);
        await context.SaveChangesAsync();

        var service = new EnrollmentService(context);
        await service.CreateEnrollmentAsync(student.Id, classA.Id, year.StartDate);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.CreateEnrollmentAsync(student.Id, classB.Id, year.StartDate));
    }

    [Fact]
    public async Task CreateEnrollmentAsync_Throws_WhenClassInstanceDoesNotExist()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        context.Add(student);
        await context.SaveChangesAsync();

        var service = new EnrollmentService(context);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.CreateEnrollmentAsync(student.Id, classInstanceId: 999, DateTime.Now));
    }
}
