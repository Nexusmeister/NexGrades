using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

public class GradeSheetStudentGradesTests
{
    private static readonly DateOnly Day = new(2025, 9, 15);

    private static readonly DateTime Now = new(2025, 10, 1, 10, 0, 0);

    private static GradeSheetService CreateService(SqliteTestDatabase db) =>
        new(db.CreateContextFactory(), new FixedClock(Now));

    /// <summary>Adds a second subject instance with one bucket to the seeded class and returns the ids.</summary>
    private static async Task<(int SubjectInstanceId, int BucketId)> AddSubjectAsync(
        SqliteTestDatabase db, GradeSheetSeed seed, string name, decimal weight = 100m)
    {
        await using var context = db.CreateContext();
        var subject = new Subject { Name = name, ShortCode = name[..2].ToUpperInvariant() };
        var subjectInstance = new SubjectInstance { ClassInstanceId = seed.ClassInstanceId, Subject = subject };
        var bucket = new GradeBucket { SubjectInstance = subjectInstance, Name = "Written", Weight = weight, SortOrder = 1 };
        context.AddRange(subject, subjectInstance, bucket);
        await context.SaveChangesAsync();
        return (subjectInstance.Id, bucket.Id);
    }

    private static StudentSubjectGrades Single(StudentGradeOverview overview) => Assert.Single(overview.Subjects);

    [Fact]
    public async Task GetStudentGradesAsync_GroupsGradesByBucket_WithinTheSubject()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, bucketWeight: 60m);
        var oral = await seed.AddBucketAsync("Oral", 40m, sortOrder: 2);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var written = await seed.AddGradeAsync(student, 2.0m, Day);
        var spoken = await seed.AddGradeAsync(student, 3.0m, Day, bucketId: oral);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        var subject = Single(overview);
        Assert.Equal("Testkunde", subject.SubjectName);
        Assert.Equal([seed.BucketId, oral], [.. subject.Buckets.Select(b => b.BucketId)]);
        Assert.Equal([written], [.. subject.Buckets[0].Grades.Select(g => g.Id)]);
        Assert.Equal([spoken], [.. subject.Buckets[1].Grades.Select(g => g.Id)]);
        Assert.Equal(60m, subject.Buckets[0].Weight);
        Assert.Equal("2025/26", overview.SchoolYearLabel);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ReturnsBucketAndWeightedSubjectAverages_MatchingTheGradeSheet()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, bucketWeight: 70m);
        var oral = await seed.AddBucketAsync("Oral", 30m, sortOrder: 2);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 1.0m, Day);
        await seed.AddGradeAsync(student, 1.3m, Day);
        await seed.AddGradeAsync(student, 3.0m, Day, bucketId: oral);
        var service = CreateService(db);

        var overview = await service.GetStudentGradesAsync(student, seed.YearId);
        var sheet = await service.GetGradeSheetAsync(seed.SubjectInstanceId);

        var subject = Single(overview);
        var row = sheet.Students.Single(s => s.StudentId == student);
        Assert.Equal(1.15m, subject.Buckets[0].Average);
        Assert.Equal(3.0m, subject.Buckets[1].Average);
        // 1.15 * 0.7 + 3.0 * 0.3 = 1.705
        Assert.Equal(1.705m, subject.SubjectAverage);
        Assert.Equal(row.SubjectAverage, subject.SubjectAverage);
        Assert.Equal(row.BucketAverages[seed.BucketId], subject.Buckets[0].Average);
        Assert.Equal(row.BucketAverages[oral], subject.Buckets[1].Average);
    }

    [Fact]
    public async Task GetStudentGradesAsync_UsesTheSnapshottedBucketWeights_AfterATemplateWeightEdit()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, bucketWeight: 75m);
        var oral = await seed.AddBucketAsync("Oral", 25m, sortOrder: 2);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 4.0m, Day, bucketId: oral);
        await using (var context = db.CreateContext())
        {
            var subjectId = context.SubjectInstances.Single(si => si.Id == seed.SubjectInstanceId).SubjectId;
            context.BucketTemplates.AddRange(
                new BucketTemplate { SubjectId = subjectId, Name = "Written", Weight = 10m, SortOrder = 1 },
                new BucketTemplate { SubjectId = subjectId, Name = "Oral", Weight = 90m, SortOrder = 2 });
            await context.SaveChangesAsync();
        }

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        Assert.Equal(2.5m, Single(overview).SubjectAverage);
    }

    [Fact]
    public async Task GetStudentGradesAsync_NormalisesWeights_OverBucketsThatHaveGrades()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db, bucketWeight: 30m);
        var oral = await seed.AddBucketAsync("Oral", 30m, sortOrder: 2);
        await seed.AddBucketAsync("Empty", 40m, sortOrder: 3);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 4.0m, Day, bucketId: oral);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        var subject = Single(overview);
        Assert.Equal(3.0m, subject.SubjectAverage);
        Assert.Null(subject.Buckets[2].Average);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ReturnsNullAverages_ForASubjectWithoutGrades()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        var subject = Single(overview);
        Assert.False(subject.IsExempt);
        Assert.Null(subject.SubjectAverage);
        var bucket = Assert.Single(subject.Buckets);
        Assert.Null(bucket.Average);
        Assert.Empty(bucket.Grades);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ReturnsExemptWithNullAverages_ButStillListsGrades()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var grade = await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddExemptionAsync(student);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        var subject = Single(overview);
        Assert.True(subject.IsExempt);
        Assert.Null(subject.SubjectAverage);
        Assert.Null(subject.Buckets[0].Average);
        Assert.Equal([grade], [.. subject.Buckets[0].Grades.Select(g => g.Id)]);
    }

    [Fact]
    public async Task GetStudentGradesAsync_OnlyMarksTheExemptSubject_WhenAnotherSubjectIsParticipated()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var (otherSubject, otherBucket) = await AddSubjectAsync(db, seed, "Zeichnen");
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddExemptionAsync(student);
        await seed.AddGradeAsync(student, 2.0m, Day, bucketId: otherBucket);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        Assert.True(overview.Subjects.Single(s => s.SubjectInstanceId == seed.SubjectInstanceId).IsExempt);
        var other = overview.Subjects.Single(s => s.SubjectInstanceId == otherSubject);
        Assert.False(other.IsExempt);
        Assert.Equal(2.0m, other.SubjectAverage);
    }

    [Fact]
    public async Task GetStudentGradesAsync_OmitsAVoidedGrade_ByDefault()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var counted = await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedOn: Now);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        var bucket = Single(overview).Buckets[0];
        Assert.Equal([counted], [.. bucket.Grades.Select(g => g.Id)]);
        Assert.Equal(2.0m, bucket.Average);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ListsAVoidedGradeWithVoidedOn_ButNeverAveragesIt_WhenIncludeVoided()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var counted = await seed.AddGradeAsync(student, 2.0m, Day);
        var voided = await seed.AddGradeAsync(student, 6.0m, new DateOnly(2025, 9, 16), voidedOn: Now);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId, includeVoided: true);

        var bucket = Single(overview).Buckets[0];
        Assert.Equal([counted, voided], [.. bucket.Grades.Select(g => g.Id)]);
        Assert.Null(bucket.Grades[0].VoidedOn);
        Assert.True(bucket.Grades[0].IsCounting);
        Assert.Equal(Now, bucket.Grades[1].VoidedOn);
        Assert.False(bucket.Grades[1].IsCounting);
        Assert.Equal(2.0m, bucket.Average);
        Assert.Equal(2.0m, Single(overview).SubjectAverage);
    }

    [Fact]
    public async Task GetStudentGradesAsync_FlagsAGradeOfAVoidedAssessment_WithoutAnOwnVoidedOn_AndDoesNotCountIt()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var active = await seed.AddAssessmentAsync("Test one", Day);
        var voidedAssessment = await seed.AddAssessmentAsync("Test two", Day, voidedOn: Now);
        await seed.AddGradeAsync(student, 2.0m, Day, active);
        var inVoided = await seed.AddGradeAsync(student, 6.0m, Day, voidedAssessment);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId, includeVoided: true);

        var bucket = Single(overview).Buckets[0];
        var entry = bucket.Grades.Single(g => g.Id == inVoided);
        Assert.True(entry.IsAssessmentVoided);
        Assert.Null(entry.VoidedOn);
        Assert.False(entry.IsCounting);
        Assert.Equal("Test two", entry.AssessmentTitle);
        Assert.Equal(2.0m, bucket.Average);
    }

    [Fact]
    public async Task GetStudentGradesAsync_OmitsGradesOfAVoidedAssessment_ByDefault()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var voidedAssessment = await seed.AddAssessmentAsync("Test one", Day, voidedOn: Now);
        await seed.AddGradeAsync(student, 6.0m, Day, voidedAssessment);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        var subject = Single(overview);
        Assert.Empty(subject.Buckets[0].Grades);
        Assert.Null(subject.SubjectAverage);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ListsActiveAssessmentsPerBucket_AndNotVoidedOnes()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var later = await seed.AddAssessmentAsync("Later", new DateOnly(2025, 11, 1));
        var earlier = await seed.AddAssessmentAsync("Earlier", new DateOnly(2025, 9, 1));
        await seed.AddAssessmentAsync("Voided", Day, voidedOn: Now);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId, includeVoided: true);

        Assert.Equal([earlier, later], [.. Single(overview).Buckets[0].Assessments.Select(a => a.Id)]);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ReturnsNoSubjects_ForAStudentNotEnrolledInThatYear()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        await seed.AddGradeAsync(await seed.AddStudentAsync("Test", "Alpha"), 2.0m, Day);
        int outsider;
        await using (var context = db.CreateContext())
        {
            var student = new Student { FirstName = "Test", LastName = "Outsider" };
            context.Add(student);
            await context.SaveChangesAsync();
            outsider = student.Id;
        }

        var overview = await CreateService(db).GetStudentGradesAsync(outsider, seed.YearId);

        Assert.Empty(overview.Subjects);
        Assert.Null(overview.JoinedOn);
        Assert.Null(overview.LeftOn);
        Assert.Equal("2025/26", overview.SchoolYearLabel);
        Assert.Equal(new DateOnly(2025, 8, 1), overview.SchoolYearStart);
        Assert.Equal(new DateOnly(2026, 7, 31), overview.SchoolYearEnd);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ReturnsEnrollmentDates_ForAnEnrolledStudent()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync(
            "Test", "Alpha", joinedOn: new DateTime(2025, 9, 10), leftOn: new DateTime(2026, 1, 31));

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        Assert.Equal(new DateOnly(2025, 9, 10), overview.JoinedOn);
        Assert.Equal(new DateOnly(2026, 1, 31), overview.LeftOn);
        Assert.Equal(new DateOnly(2025, 8, 1), overview.SchoolYearStart);
        Assert.Equal(new DateOnly(2026, 7, 31), overview.SchoolYearEnd);
    }

    [Fact]
    public async Task GetStudentGradesAsync_Throws_ForAnUnknownStudent()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).GetStudentGradesAsync(9999, seed.YearId));
    }

    [Fact]
    public async Task GetStudentGradesAsync_Throws_ForAnUnknownSchoolYear()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => CreateService(db).GetStudentGradesAsync(student, 9999));
    }

    [Fact]
    public async Task GetStudentGradesAsync_ReturnsTheGrades_ForALockedClosedYear()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.SetYearStatusAsync(SchoolYearStatus.Closed);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        Assert.Equal(2.0m, Single(overview).SubjectAverage);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ReturnsOnlyThisStudentsGrades_NotClassmates()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var classmate = await seed.AddStudentAsync("Test", "Beta");
        var own = await seed.AddGradeAsync(student, 2.0m, Day);
        await seed.AddGradeAsync(classmate, 5.0m, Day);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        var bucket = Single(overview).Buckets[0];
        Assert.Equal([own], [.. bucket.Grades.Select(g => g.Id)]);
        Assert.Equal(2.0m, bucket.Average);
    }

    [Fact]
    public async Task GetStudentGradesAsync_OrdersSubjectsByName_BucketsBySortOrder_AndGradesByDateThenId()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var (zeichnen, _) = await AddSubjectAsync(db, seed, "Aardvark");
        var oral = await seed.AddBucketAsync("Oral", 40m, sortOrder: 0);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var lateDate = await seed.AddGradeAsync(student, 2.0m, new DateOnly(2025, 10, 1));
        var earlyFirst = await seed.AddGradeAsync(student, 3.0m, new DateOnly(2025, 9, 1));
        var earlySecond = await seed.AddGradeAsync(student, 4.0m, new DateOnly(2025, 9, 1));

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        Assert.Equal(["Aardvark", "Testkunde"], [.. overview.Subjects.Select(s => s.SubjectName)]);
        Assert.Equal(zeichnen, overview.Subjects[0].SubjectInstanceId);
        var testkunde = overview.Subjects[1];
        Assert.Equal([oral, seed.BucketId], [.. testkunde.Buckets.Select(b => b.BucketId)]);
        Assert.Equal([earlyFirst, earlySecond, lateDate], [.. testkunde.Buckets[1].Grades.Select(g => g.Id)]);
    }

    [Fact]
    public async Task GetStudentGradesAsync_FallsBackToTheInvariantValue_ForAnOffScaleLegacyGrade()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        await seed.AddGradeAsync(student, 2.5m, Day);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        var entry = Assert.Single(Single(overview).Buckets[0].Grades);
        Assert.Equal("2.5", entry.Token);
        Assert.Equal(2.5m, entry.Value);
    }

    [Fact]
    public async Task GetStudentGradesAsync_ReturnsTheTokenLabelCommentAndAssessmentTitle_OfAGrade()
    {
        using var db = new SqliteTestDatabase();
        var seed = await GradeSheetSeed.CreateAsync(db);
        var student = await seed.AddStudentAsync("Test", "Alpha");
        var assessment = await seed.AddAssessmentAsync("Test one", Day);
        await seed.AddGradeAsync(student, 2.3m, Day, assessment);

        var overview = await CreateService(db).GetStudentGradesAsync(student, seed.YearId);

        var entry = Assert.Single(Single(overview).Buckets[0].Grades);
        Assert.Equal("2-", entry.Token);
        Assert.Equal(assessment, entry.AssessmentId);
        Assert.Equal("Test one", entry.AssessmentTitle);
        Assert.Equal(Day, entry.RecordedOn);
        Assert.Null(entry.Label);
        Assert.Null(entry.Comment);
    }
}
