using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Export;
using NexGrades.Domain.Grading;

namespace NexGrades.Tests;

public class SubjectOverviewExportServiceTests
{
    private static readonly DateTime SchoolStart = new(2025, 8, 1);

    private static async Task<Fixture> SeedAsync(SqliteTestDatabase db, SchoolYearStatus status = SchoolYearStatus.Active)
    {
        await using var context = db.CreateContext();
        var year = new SchoolYear
        {
            Label = "2025/26",
            StartDate = SchoolStart,
            EndDate = new DateTime(2026, 7, 31),
            Status = status,
        };
        var group = new ClassGroup { CohortLabel = "Test cohort", FoundedInSchoolYear = year };
        var classInstance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A" };
        var subject = new Subject { Name = "Testkunde", ShortCode = "TK" };
        var subjectInstance = new SubjectInstance { ClassInstance = classInstance, Subject = subject };
        context.AddRange(year, group, classInstance, subject, subjectInstance);
        await context.SaveChangesAsync();
        return new Fixture(db, classInstance.Id, subject.Id, subjectInstance.Id);
    }

    private sealed class Fixture(SqliteTestDatabase db, int classInstanceId, int subjectId, int subjectInstanceId)
    {
        public int ClassInstanceId { get; } = classInstanceId;

        public int SubjectId { get; } = subjectId;

        public int SubjectInstanceId { get; } = subjectInstanceId;

        public async Task<int> AddBucketAsync(string name, decimal weight, int sortOrder)
        {
            await using var context = db.CreateContext();
            var bucket = new GradeBucket { SubjectInstanceId = SubjectInstanceId, Name = name, Weight = weight, SortOrder = sortOrder };
            context.Add(bucket);
            await context.SaveChangesAsync();
            return bucket.Id;
        }

        public async Task<int> AddStudentAsync(string first, string last, DateTime? leftOn = null, int? classInstanceId = null)
        {
            await using var context = db.CreateContext();
            var student = new Student { FirstName = first, LastName = last };
            context.Add(student);
            context.Add(new Enrollment
            {
                Student = student,
                ClassInstanceId = classInstanceId ?? ClassInstanceId,
                JoinedOn = SchoolStart,
                LeftOn = leftOn,
            });
            await context.SaveChangesAsync();
            return student.Id;
        }

        public async Task AddGradeAsync(int bucketId, int studentId, decimal value, DateOnly? recordedOn = null)
        {
            await using var context = db.CreateContext();
            context.Add(new Grade
            {
                GradeBucketId = bucketId,
                StudentId = studentId,
                Value = value,
                RecordedOn = recordedOn ?? new DateOnly(2025, 9, 15),
            });
            await context.SaveChangesAsync();
        }

        public async Task AddParticipationAsync(int studentId, ParticipationMode mode)
        {
            await using var context = db.CreateContext();
            context.Add(new SubjectParticipation { SubjectInstanceId = SubjectInstanceId, StudentId = studentId, Mode = mode });
            await context.SaveChangesAsync();
        }
    }

    private static Task<SubjectOverviewTable> BuildAsync(SqliteTestDatabase db, Fixture fixture) =>
        new SubjectOverviewExportService(db.CreateContextFactory()).BuildAsync(fixture.SubjectInstanceId);

    private static string[] Names(SubjectOverviewTable table) =>
        [.. table.Rows.Select(r => r.Cells[0].Text!)];

    [Fact]
    public async Task BuildAsync_ReturnsHeaderData_ForSubjectInstance()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);

        var table = await BuildAsync(db, fixture);

        Assert.Equal("Testkunde", table.SubjectName);
        Assert.Equal("5A", table.ClassName);
        Assert.Equal("2025/26", table.SchoolYearLabel);
    }

    [Fact]
    public async Task BuildAsync_ReturnsStudentBucketPairsAndSubjectAverageColumns_InSortOrder()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await fixture.AddBucketAsync("Oral", 40m, sortOrder: 2);
        await fixture.AddBucketAsync("Written", 60m, sortOrder: 1);

        var table = await BuildAsync(db, fixture);

        Assert.Equal(
            new (SubjectOverviewColumnKind, string?)[]
            {
                (SubjectOverviewColumnKind.Student, null),
                (SubjectOverviewColumnKind.BucketGrades, "Written"),
                (SubjectOverviewColumnKind.BucketAverage, "Written"),
                (SubjectOverviewColumnKind.BucketGrades, "Oral"),
                (SubjectOverviewColumnKind.BucketAverage, "Oral"),
                (SubjectOverviewColumnKind.SubjectAverage, null),
            },
            table.Columns.Select(c => (c.Kind, c.BucketName)).ToArray());
    }

    [Fact]
    public async Task BuildAsync_ReturnsNoRows_ForSubjectWithoutStudents()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await fixture.AddBucketAsync("Written", 100m, 1);

        var table = await BuildAsync(db, fixture);

        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task BuildAsync_ReturnsEmptyCellsNotZero_ForSubjectWithoutGrades()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await fixture.AddBucketAsync("Written", 100m, 1);
        await fixture.AddStudentAsync("Test", "Alpha");

        var table = await BuildAsync(db, fixture);

        var row = Assert.Single(table.Rows);
        Assert.Equal("Alpha, Test", row.Cells[0].Text);
        Assert.Equal(SubjectOverviewCell.Empty, row.Cells[1]);
        Assert.Equal(SubjectOverviewCell.Empty, row.Cells[2]);
        Assert.Equal(SubjectOverviewCell.Empty, row.Cells[3]);
        Assert.Null(row.LeftOn);
    }

    [Fact]
    public async Task BuildAsync_ReturnsGradeTokensAndAverages_ForStudentWithGrades()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        var student = await fixture.AddStudentAsync("Test", "Alpha");
        await fixture.AddGradeAsync(bucket, student, 2.0m);
        await fixture.AddGradeAsync(bucket, student, 3.0m, new DateOnly(2025, 9, 16));

        var row = Assert.Single((await BuildAsync(db, fixture)).Rows);

        Assert.Equal("2; 3", row.Cells[1].Text);
        Assert.Equal(2.5m, row.Cells[2].Number);
        Assert.Equal(2.5m, row.Cells[3].Number);
    }

    [Fact]
    public async Task BuildAsync_JoinsTokensInRecordedOnOrder_NotInsertionOrder()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        var student = await fixture.AddStudentAsync("Test", "Alpha");
        await fixture.AddGradeAsync(bucket, student, 1.7m, new DateOnly(2025, 10, 1));
        await fixture.AddGradeAsync(bucket, student, 3.0m, new DateOnly(2025, 11, 1));
        await fixture.AddGradeAsync(bucket, student, 2.3m, new DateOnly(2025, 9, 1));

        var row = Assert.Single((await BuildAsync(db, fixture)).Rows);

        Assert.Equal("2-; 2+; 3", row.Cells[1].Text);
    }

    [Fact]
    public async Task BuildAsync_OrdersSameDayGradesById()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        var student = await fixture.AddStudentAsync("Test", "Alpha");
        await fixture.AddGradeAsync(bucket, student, 0.7m);
        await fixture.AddGradeAsync(bucket, student, 2.0m);
        await fixture.AddGradeAsync(bucket, student, 1.0m);

        var row = Assert.Single((await BuildAsync(db, fixture)).Rows);

        Assert.Equal("1+; 2; 1", row.Cells[1].Text);
    }

    [Fact]
    public async Task BuildAsync_KeepsDecimalPrecision_ForAverage()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        var student = await fixture.AddStudentAsync("Test", "Alpha");
        await fixture.AddGradeAsync(bucket, student, 1.0m);
        await fixture.AddGradeAsync(bucket, student, 1.0m);
        await fixture.AddGradeAsync(bucket, student, 1.3m);

        var row = Assert.Single((await BuildAsync(db, fixture)).Rows);

        Assert.Equal(AverageCalculator.BucketAverage([1.0m, 1.0m, 1.3m]), row.Cells[2].Number);
    }

    [Fact]
    public async Task BuildAsync_LeavesEmptyBucketEmpty_AndSkipsItInSubjectAverage()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var written = await fixture.AddBucketAsync("Written", 60m, 1);
        await fixture.AddBucketAsync("Oral", 40m, 2);
        var student = await fixture.AddStudentAsync("Test", "Alpha");
        await fixture.AddGradeAsync(written, student, 2.0m);

        var row = Assert.Single((await BuildAsync(db, fixture)).Rows);

        Assert.Equal(SubjectOverviewCell.Empty, row.Cells[3]);
        Assert.Equal(SubjectOverviewCell.Empty, row.Cells[4]);
        Assert.Equal(
            AverageCalculator.SubjectAverage(
            [
                new AverageCalculator.WeightedBucketAverage(60m, 2.0m),
                new AverageCalculator.WeightedBucketAverage(40m, null),
            ]),
            row.Cells[5].Number);
        Assert.Equal(2.0m, row.Cells[5].Number);
    }

    [Fact]
    public async Task BuildAsync_WeightsSubjectAverageBySnapshottedBucketWeight()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var written = await fixture.AddBucketAsync("Written", 75m, 1);
        var oral = await fixture.AddBucketAsync("Oral", 25m, 2);
        var student = await fixture.AddStudentAsync("Test", "Alpha");
        await fixture.AddGradeAsync(written, student, 2.0m);
        await fixture.AddGradeAsync(oral, student, 4.0m);

        // Templates with other weights, created after the snapshot, must not change the result.
        await using (var context = db.CreateContext())
        {
            context.AddRange(
                new BucketTemplate { SubjectId = fixture.SubjectId, Name = "Written", Weight = 10m, SortOrder = 1 },
                new BucketTemplate { SubjectId = fixture.SubjectId, Name = "Oral", Weight = 90m, SortOrder = 2 });
            await context.SaveChangesAsync();
        }

        var row = Assert.Single((await BuildAsync(db, fixture)).Rows);

        Assert.Equal(2.5m, row.Cells[5].Number);
    }

    [Fact]
    public async Task BuildAsync_ExcludesExemptStudent()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await fixture.AddBucketAsync("Written", 100m, 1);
        await fixture.AddStudentAsync("Test", "Alpha");
        var exempt = await fixture.AddStudentAsync("Test", "Beta");
        await fixture.AddParticipationAsync(exempt, ParticipationMode.Exempt);

        var table = await BuildAsync(db, fixture);

        Assert.Equal(["Alpha, Test"], Names(table));
    }

    [Fact]
    public async Task BuildAsync_ExcludesExemptStudent_EvenWithGrades()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        var exempt = await fixture.AddStudentAsync("Test", "Beta");
        await fixture.AddGradeAsync(bucket, exempt, 2.0m);
        await fixture.AddParticipationAsync(exempt, ParticipationMode.Exempt);

        var table = await BuildAsync(db, fixture);

        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task BuildAsync_IncludesStudent_WithParticipatingOverride()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await fixture.AddBucketAsync("Written", 100m, 1);
        var student = await fixture.AddStudentAsync("Test", "Alpha");
        await fixture.AddParticipationAsync(student, ParticipationMode.Participating);

        var table = await BuildAsync(db, fixture);

        Assert.Equal(["Alpha, Test"], Names(table));
    }

    [Fact]
    public async Task BuildAsync_IncludesLeftStudentWithGradesAtEnd_WithLeftOn()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        var left = await fixture.AddStudentAsync("Test", "Aaa", leftOn: new DateTime(2025, 12, 1));
        await fixture.AddStudentAsync("Test", "Zzz");
        await fixture.AddGradeAsync(bucket, left, 2.0m);

        var table = await BuildAsync(db, fixture);

        Assert.Equal(["Zzz, Test", "Aaa, Test"], Names(table));
        Assert.Null(table.Rows[0].LeftOn);
        Assert.Equal(new DateOnly(2025, 12, 1), table.Rows[1].LeftOn);
    }

    [Fact]
    public async Task BuildAsync_ExcludesLeftStudent_WithoutGrades()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await fixture.AddBucketAsync("Written", 100m, 1);
        await fixture.AddStudentAsync("Test", "Aaa", leftOn: new DateTime(2025, 12, 1));
        await fixture.AddStudentAsync("Test", "Zzz");

        var table = await BuildAsync(db, fixture);

        Assert.Equal(["Zzz, Test"], Names(table));
    }

    [Fact]
    public async Task BuildAsync_ExcludesLeftExemptStudent_EvenWithGrades()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        var left = await fixture.AddStudentAsync("Test", "Aaa", leftOn: new DateTime(2025, 12, 1));
        await fixture.AddGradeAsync(bucket, left, 2.0m);
        await fixture.AddParticipationAsync(left, ParticipationMode.Exempt);

        var table = await BuildAsync(db, fixture);

        Assert.Empty(table.Rows);
    }

    [Fact]
    public async Task BuildAsync_SortsActiveStudentsByLastThenFirstName()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        await fixture.AddBucketAsync("Written", 100m, 1);
        await fixture.AddStudentAsync("Zed", "Beta");
        await fixture.AddStudentAsync("Amy", "Beta");
        await fixture.AddStudentAsync("Test", "Alpha");

        var table = await BuildAsync(db, fixture);

        Assert.Equal(["Alpha, Test", "Beta, Amy", "Beta, Zed"], Names(table));
    }

    [Fact]
    public async Task BuildAsync_ExcludesStudentsOfAnotherClassInstance()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        await fixture.AddStudentAsync("Test", "Alpha");

        int otherClassId;
        await using (var context = db.CreateContext())
        {
            var year = context.SchoolYears.Single();
            var group = new ClassGroup { CohortLabel = "Other cohort", FoundedInSchoolYearId = year.Id };
            var other = new ClassInstance { ClassGroup = group, SchoolYearId = year.Id, Name = "5B" };
            context.AddRange(group, other);
            await context.SaveChangesAsync();
            otherClassId = other.Id;
        }

        var outsider = await fixture.AddStudentAsync("Test", "Outsider", classInstanceId: otherClassId);
        await fixture.AddGradeAsync(bucket, outsider, 1.0m);

        var table = await BuildAsync(db, fixture);

        Assert.Equal(["Alpha, Test"], Names(table));
    }

    [Fact]
    public async Task BuildAsync_FallsBackToInvariantText_ForOffScaleValue()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        var student = await fixture.AddStudentAsync("Test", "Alpha");
        await fixture.AddGradeAsync(bucket, student, 2.5m, new DateOnly(2025, 9, 1));
        await fixture.AddGradeAsync(bucket, student, 2.0m, new DateOnly(2025, 9, 2));

        var row = Assert.Single((await BuildAsync(db, fixture)).Rows);

        Assert.Equal("2.5; 2", row.Cells[1].Text);
        Assert.Equal(2.25m, row.Cells[2].Number);
    }

    [Fact]
    public async Task BuildAsync_Works_ForClosedYearAndWritesNothing()
    {
        using var db = new SqliteTestDatabase();
        var fixture = await SeedAsync(db, SchoolYearStatus.Closed);
        var bucket = await fixture.AddBucketAsync("Written", 100m, 1);
        var student = await fixture.AddStudentAsync("Test", "Alpha");
        await fixture.AddGradeAsync(bucket, student, 2.0m);

        var table = await BuildAsync(db, fixture);

        var row = Assert.Single(table.Rows);
        Assert.Equal(2.0m, row.Cells[3].Number);
        await using var context = db.CreateContext();
        Assert.Empty(context.AuditEntries);
        Assert.Equal(1, context.Grades.Count());
        Assert.Equal(1, context.Students.Count());
        Assert.Equal(1, context.GradeBuckets.Count());
    }

    [Fact]
    public async Task BuildAsync_Throws_ForUnknownSubjectInstance()
    {
        using var db = new SqliteTestDatabase();
        await SeedAsync(db);
        var service = new SubjectOverviewExportService(db.CreateContextFactory());

        await Assert.ThrowsAsync<InvariantViolationException>(() => service.BuildAsync(9999));

        await using var context = db.CreateContext();
        Assert.Empty(context.AuditEntries);
    }
}
