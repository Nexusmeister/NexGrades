using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class RolloverServiceTests
{
    [Fact]
    public async Task RollClassInstanceForwardAsync_MarksOldInstanceRolledForward_AndCreatesNewOne()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var yearOld = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var yearNew = new SchoolYear { Label = "2026/27", StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2027, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = yearOld };
        var oldInstance = new ClassInstance { ClassGroup = group, SchoolYear = yearOld, Name = "5A" };
        var student = new Student { FirstName = "Anna", LastName = "Schmidt" };
        var enrollment = new Enrollment { Student = student, ClassInstance = oldInstance, JoinedOn = yearOld.StartDate };
        context.AddRange(yearOld, yearNew, group, oldInstance, student, enrollment);
        await context.SaveChangesAsync();

        var clock = new FixedClock(new DateTime(2026, 7, 15));
        var service = new RolloverService(context, clock);
        var newInstance = await service.RollClassInstanceForwardAsync(oldInstance.Id, yearNew.Id, "6A");

        var refreshedOld = await context.ClassInstances.FirstAsync(ci => ci.Id == oldInstance.Id);
        Assert.Equal(ClassInstanceStatus.RolledForward, refreshedOld.Status);

        Assert.Equal("6A", newInstance.Name);
        Assert.Equal(group.Id, newInstance.ClassGroupId);
        Assert.Equal(yearNew.Id, newInstance.SchoolYearId);
        Assert.Equal(ClassInstanceStatus.Active, newInstance.Status);

        var newEnrollments = await context.Enrollments.Where(e => e.ClassInstanceId == newInstance.Id).ToListAsync();
        Assert.Single(newEnrollments);
        Assert.Equal(student.Id, newEnrollments[0].StudentId);
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_CopiesBucketsFromCurrentTemplate_NotFromOldSnapshot()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var yearOld = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var yearNew = new SchoolYear { Label = "2026/27", StartDate = new DateTime(2026, 8, 1), EndDate = new DateTime(2027, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = yearOld };
        var oldInstance = new ClassInstance { ClassGroup = group, SchoolYear = yearOld, Name = "5A" };
        var subject = new Subject { Name = "Mathematik", ShortCode = "MA" };
        var template = new BucketTemplate { Subject = subject, Name = "Schriftlich", Weight = 60m };
        var subjectInstance = new SubjectInstance { ClassInstance = oldInstance, Subject = subject };
        // Old snapshot deliberately diverges from the (later-edited) template, to prove rollover uses the template.
        var oldBucket = new GradeBucket { SubjectInstance = subjectInstance, Name = "Schriftlich", Weight = 50m };
        context.AddRange(yearOld, yearNew, group, oldInstance, subject, template, subjectInstance, oldBucket);
        await context.SaveChangesAsync();

        // Template is edited after the old instance's snapshot was taken.
        template.Weight = 80m;
        await context.SaveChangesAsync();

        var clock = new FixedClock(new DateTime(2026, 7, 15));
        var service = new RolloverService(context, clock);
        var newInstance = await service.RollClassInstanceForwardAsync(oldInstance.Id, yearNew.Id, "6A");

        var newBuckets = await context.GradeBuckets
            .Where(gb => gb.SubjectInstance.ClassInstanceId == newInstance.Id)
            .ToListAsync();

        Assert.Single(newBuckets);
        Assert.Equal(80m, newBuckets[0].Weight); // current template weight, not the old 50m snapshot.

        var refreshedOldBucket = await context.GradeBuckets.FirstAsync(gb => gb.Id == oldBucket.Id);
        Assert.Equal(50m, refreshedOldBucket.Weight); // old snapshot itself is untouched.
    }

    [Fact]
    public async Task RollClassInstanceForwardAsync_Throws_WhenInstanceIsNotActive()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A", Status = ClassInstanceStatus.Archived };
        context.AddRange(year, group, instance);
        await context.SaveChangesAsync();

        var service = new RolloverService(context, new FixedClock(DateTime.Now));

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.RollClassInstanceForwardAsync(instance.Id, year.Id, "6A"));
    }

    [Fact]
    public async Task ArchiveClassInstanceAsync_UnassignsTeacher_AndOptionallyEndsClassGroup()
    {
        using var db = new SqliteTestDatabase();
        await using var context = db.CreateContext();

        var year = new SchoolYear { Label = "2025/26", StartDate = new DateTime(2025, 8, 1), EndDate = new DateTime(2026, 7, 31) };
        var teacher = new Teacher { DisplayName = "Frau Meier" };
        var group = new ClassGroup { CohortLabel = "Kohorte 2019", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "9A", Teacher = teacher };
        context.AddRange(year, teacher, group, instance);
        await context.SaveChangesAsync();

        var service = new RolloverService(context, new FixedClock(DateTime.Now));
        await service.ArchiveClassInstanceAsync(instance.Id, endClassGroup: true);

        var refreshedInstance = await context.ClassInstances.FirstAsync(ci => ci.Id == instance.Id);
        var refreshedGroup = await context.ClassGroups.FirstAsync(g => g.Id == group.Id);

        Assert.Equal(ClassInstanceStatus.Archived, refreshedInstance.Status);
        Assert.Null(refreshedInstance.TeacherId);
        Assert.Equal(ClassGroupStatus.Ended, refreshedGroup.Status);
    }
}
