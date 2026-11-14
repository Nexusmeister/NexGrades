using NexGrades.Data.Entities;

namespace NexGrades.Tests;

/// <summary>
/// Seeds one school year (2025-08-01 to 2026-07-31) with one class, one subject instance and one bucket, and
/// adds students, assessments and grades to it. Shared by the assessment and grade sheet tests so each test
/// seeds only what it needs. Writes go through plain contexts, so a database built with the closed-year
/// interceptor is seeded while the year is still active and closed afterwards with
/// <see cref="SetYearStatusAsync"/>.
/// </summary>
internal sealed class GradeSheetSeed
{
    public static readonly DateTime YearStart = new(2025, 8, 1);

    public static readonly DateTime YearEnd = new(2026, 7, 31);

    private readonly SqliteTestDatabase _db;

    private GradeSheetSeed(SqliteTestDatabase db) => _db = db;

    public int YearId { get; private set; }

    public int ClassInstanceId { get; private set; }

    public int SubjectInstanceId { get; private set; }

    public int BucketId { get; private set; }

    public static async Task<GradeSheetSeed> CreateAsync(
        SqliteTestDatabase db, SchoolYearStatus status = SchoolYearStatus.Active, decimal bucketWeight = 100m)
    {
        await using var context = db.CreateContext();
        var year = new SchoolYear { Label = "2025/26", StartDate = YearStart, EndDate = YearEnd, Status = status };
        var group = new ClassGroup { CohortLabel = "Test cohort", FoundedInSchoolYear = year };
        var classInstance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A" };
        var subject = new Subject { Name = "Testkunde", ShortCode = "TK" };
        var subjectInstance = new SubjectInstance { ClassInstance = classInstance, Subject = subject };
        var bucket = new GradeBucket { SubjectInstance = subjectInstance, Name = "Written", Weight = bucketWeight, SortOrder = 1 };
        context.AddRange(year, group, classInstance, subject, subjectInstance, bucket);
        await context.SaveChangesAsync();

        return new GradeSheetSeed(db)
        {
            YearId = year.Id,
            ClassInstanceId = classInstance.Id,
            SubjectInstanceId = subjectInstance.Id,
            BucketId = bucket.Id,
        };
    }

    public async Task SetYearStatusAsync(SchoolYearStatus status, DateTime? unlockedUntil = null, string? unlockReason = null)
    {
        await using var context = _db.CreateContext();
        var year = await context.SchoolYears.FindAsync(YearId);
        year!.Status = status;
        year.UnlockedUntil = unlockedUntil;
        year.UnlockReason = unlockReason;
        await context.SaveChangesAsync();
    }

    public async Task<int> AddBucketAsync(string name, decimal weight, int sortOrder)
    {
        await using var context = _db.CreateContext();
        var bucket = new GradeBucket { SubjectInstanceId = SubjectInstanceId, Name = name, Weight = weight, SortOrder = sortOrder };
        context.Add(bucket);
        await context.SaveChangesAsync();
        return bucket.Id;
    }

    public async Task<int> AddStudentAsync(
        string first, string last, DateTime? joinedOn = null, DateTime? leftOn = null)
    {
        await using var context = _db.CreateContext();
        var student = new Student { FirstName = first, LastName = last };
        context.Add(student);
        context.Add(new Enrollment
        {
            Student = student,
            ClassInstanceId = ClassInstanceId,
            JoinedOn = joinedOn ?? YearStart,
            LeftOn = leftOn,
        });
        await context.SaveChangesAsync();
        return student.Id;
    }

    public async Task AddExemptionAsync(int studentId)
    {
        await using var context = _db.CreateContext();
        context.Add(new SubjectParticipation { SubjectInstanceId = SubjectInstanceId, StudentId = studentId, Mode = ParticipationMode.Exempt });
        await context.SaveChangesAsync();
    }

    public async Task<int> AddAssessmentAsync(string title, DateOnly date, int? bucketId = null, DateTime? voidedOn = null)
    {
        await using var context = _db.CreateContext();
        var assessment = new Assessment { GradeBucketId = bucketId ?? BucketId, Title = title, Date = date, VoidedOn = voidedOn };
        context.Add(assessment);
        await context.SaveChangesAsync();
        return assessment.Id;
    }

    public async Task<int> AddGradeAsync(
        int studentId, decimal value, DateOnly recordedOn, int? assessmentId = null, int? bucketId = null, DateTime? voidedOn = null)
    {
        await using var context = _db.CreateContext();
        var effectiveBucketId = bucketId ?? BucketId;
        if (assessmentId is { } id)
        {
            effectiveBucketId = (await context.Assessments.FindAsync(id))!.GradeBucketId;
        }

        var grade = new Grade
        {
            GradeBucketId = effectiveBucketId,
            AssessmentId = assessmentId,
            StudentId = studentId,
            Value = value,
            RecordedOn = recordedOn,
            VoidedOn = voidedOn,
        };
        context.Add(grade);
        await context.SaveChangesAsync();
        return grade.Id;
    }
}
