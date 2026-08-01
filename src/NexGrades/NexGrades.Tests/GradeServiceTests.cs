using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class GradeServiceTests
{
    private static async Task<(NexGrades.Data.AppDbContext Context, GradeBucket Bucket, Student Student, SubjectInstance SubjectInstance)> SeedAsync(
        SqliteTestDatabase db, DateTime enrolledFrom, DateTime? enrolledTo = null)
    {
        var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var classInstance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A" };
        var subject = new Subject { Name = "Mathematik", ShortCode = "MA" };
        var subjectInstance = new SubjectInstance { ClassInstance = classInstance, Subject = subject };
        var bucket = new GradeBucket { SubjectInstance = subjectInstance, Name = "Schriftlich", Weight = 100m };
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        var enrollment = new Enrollment { Student = student, ClassInstance = classInstance, JoinedOn = enrolledFrom, LeftOn = enrolledTo };

        context.AddRange(year, group, classInstance, subject, subjectInstance, bucket, student, enrollment);
        await context.SaveChangesAsync();

        return (context, bucket, student, subjectInstance);
    }

    [Fact]
    public async Task RecordGradeAsync_Succeeds_ForAnEnrolledParticipatingStudent()
    {
        using var db = new SqliteTestDatabase();
        var (context, bucket, student, _) = await SeedAsync(db, enrolledFrom: new DateTime(2025, 8, 1));
        await using var _ = context;

        var service = new GradeService(context);
        var grade = await service.RecordGradeAsync(bucket.Id, student.Id, 2.0m, new DateOnly(2025, 9, 15));

        Assert.NotEqual(0, grade.Id);
        Assert.Equal(2.0m, grade.Value);
    }

    [Fact]
    public async Task RecordGradeAsync_Throws_ForIllegalGradeValue()
    {
        using var db = new SqliteTestDatabase();
        var (context, bucket, student, _) = await SeedAsync(db, enrolledFrom: new DateTime(2025, 8, 1));
        await using var _ = context;

        var service = new GradeService(context);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.RecordGradeAsync(bucket.Id, student.Id, 2.5m, new DateOnly(2025, 9, 15)));
    }

    [Fact]
    public async Task RecordGradeAsync_Throws_WhenStudentNotYetEnrolledOnRecordedDate()
    {
        using var db = new SqliteTestDatabase();
        var (context, bucket, student, _) = await SeedAsync(db, enrolledFrom: new DateTime(2025, 10, 1));
        await using var _ = context;

        var service = new GradeService(context);

        // RecordedOn is before the enrollment even started.
        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.RecordGradeAsync(bucket.Id, student.Id, 2.0m, new DateOnly(2025, 9, 1)));
    }

    [Fact]
    public async Task RecordGradeAsync_Throws_WhenStudentHasSinceLeftTheClass()
    {
        using var db = new SqliteTestDatabase();
        var (context, bucket, student, _) = await SeedAsync(
            db, enrolledFrom: new DateTime(2025, 8, 1), enrolledTo: new DateTime(2025, 12, 1));
        await using var _ = context;

        var service = new GradeService(context);

        // RecordedOn is after LeftOn.
        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.RecordGradeAsync(bucket.Id, student.Id, 2.0m, new DateOnly(2026, 1, 15)));
    }

    [Fact]
    public async Task RecordGradeAsync_Throws_WhenStudentIsExemptFromTheSubject()
    {
        using var db = new SqliteTestDatabase();
        var (context, bucket, student, subjectInstance) = await SeedAsync(db, enrolledFrom: new DateTime(2025, 8, 1));
        await using var _ = context;

        context.SubjectParticipations.Add(new SubjectParticipation
        {
            SubjectInstance = subjectInstance,
            Student = student,
            Mode = ParticipationMode.Exempt,
        });
        await context.SaveChangesAsync();

        var service = new GradeService(context);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.RecordGradeAsync(bucket.Id, student.Id, 2.0m, new DateOnly(2025, 9, 15)));
    }
}
