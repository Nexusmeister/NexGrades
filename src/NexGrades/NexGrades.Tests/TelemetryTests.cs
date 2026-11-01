using System.Collections.Concurrent;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NexGrades.Data;
using NexGrades.Data.Diagnostics;
using NexGrades.Data.Entities;
using NexGrades.Data.Services;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Services;

namespace NexGrades.Tests;

[Collection(NonParallelTelemetry.Name)]
public class TelemetryTests
{
    private static readonly DateTime Now = new(2026, 3, 10, 9, 0, 0);

    private const string AddNoteOperation = "NoteService.AddNote";
    private const string MigrationOperation = "DatabaseMigrationService.Run";

    private sealed record Measurement(double Value, Dictionary<string, object?> Tags);

    /// <summary>Records every NexGrades span and every <c>nexgrades.operation.duration</c> measurement.</summary>
    private sealed class TelemetryCapture : IDisposable
    {
        private readonly ActivityListener _activityListener;
        private readonly MeterListener _meterListener;
        private readonly ConcurrentQueue<Activity> _activities = new();
        private readonly ConcurrentQueue<Measurement> _measurements = new();
        private readonly ConcurrentQueue<Measurement> _otherMeasurements = new();

        public TelemetryCapture()
        {
            _activityListener = new ActivityListener
            {
                ShouldListenTo = source => source.Name == NexGradesTelemetry.Name,
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = _activities.Enqueue,
            };
            ActivitySource.AddActivityListener(_activityListener);

            _meterListener = new MeterListener
            {
                InstrumentPublished = (instrument, listener) =>
                {
                    if (instrument.Meter.Name == NexGradesTelemetry.Name)
                    {
                        listener.EnableMeasurementEvents(instrument);
                    }
                },
            };
            _meterListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
            {
                var captured = new Measurement(value, tags.ToArray().ToDictionary(t => t.Key, t => t.Value));
                if (instrument.Name == NexGradesTelemetry.OperationDuration.Name)
                {
                    _measurements.Enqueue(captured);
                }
                else
                {
                    _otherMeasurements.Enqueue(captured);
                }
            });
            _meterListener.Start();
        }

        public IReadOnlyList<Activity> SpansFor(string operation) =>
            [.. _activities.Where(a => a.OperationName == operation)];

        public IReadOnlyList<Measurement> DurationsFor(string operation) =>
            [.. _measurements.Where(m => Equals(m.Tags.GetValueOrDefault(NexGradesTelemetry.OperationTag), operation))];

        public IReadOnlyList<Activity> AllSpans => [.. _activities];

        public IReadOnlyList<Measurement> AllMeasurements => [.. _measurements, .. _otherMeasurements];

        public void Dispose()
        {
            _activityListener.Dispose();
            _meterListener.Dispose();
        }
    }

    private sealed class Env : IDisposable
    {
        public SqliteTestDatabase Db { get; }
        public FixedClock Clock { get; }
        public UnlockReasonStore Reasons { get; } = new();
        public NoteService Service { get; }

        public Env()
        {
            Clock = new FixedClock(Now);
            Db = new SqliteTestDatabase(new ClosedYearAuditInterceptor(Clock, Reasons));
            Service = new NoteService(Db.CreateContextFactory(), Clock);
        }

        public void Dispose() => Db.Dispose();
    }

    private sealed record Seed(int StudentId, int YearId);

    private static async Task<Seed> SeedAsync(
        Env env,
        SchoolYearStatus status = SchoolYearStatus.Active,
        string firstName = "Anna")
    {
        // A context without the interceptor, so seeding is allowed in any year state.
        await using var context = new AppDbContext(
            new DbContextOptionsBuilder<AppDbContext>()
                .UseSqlite($"Data Source={env.Db.DbPath}").Options);
        var year = new SchoolYear
        {
            Label = "2025/26",
            StartDate = new DateTime(2025, 8, 1),
            EndDate = new DateTime(2026, 7, 31),
            Status = status,
            ClosedOn = status == SchoolYearStatus.Closed ? new DateTime(2026, 7, 31) : null,
        };
        var group = new ClassGroup { CohortLabel = "Kohorte 2025", FoundedInSchoolYear = year };
        var instance = new ClassInstance { ClassGroup = group, SchoolYear = year, Name = "5A" };
        var student = new Student { FirstName = firstName, LastName = "Schmidt" };
        context.AddRange(year, group, instance, student);
        context.Enrollments.Add(new Enrollment
        {
            Student = student,
            ClassInstance = instance,
            JoinedOn = new DateTime(2025, 8, 1),
        });

        await context.SaveChangesAsync();
        return new Seed(student.Id, year.Id);
    }

    private static string? OutcomeOf(Activity activity) =>
        activity.GetTagItem(NexGradesTelemetry.OutcomeTag) as string;

    // ---------- Outcomes ----------

    [Fact]
    public async Task AddNoteAsync_RecordsSuccessSpanAndDuration_WhenNoteIsAdded()
    {
        using var capture = new TelemetryCapture();
        using var env = new Env();
        var seed = await SeedAsync(env);

        await env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "Talked to parents");

        var span = Assert.Single(capture.SpansFor(AddNoteOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.Success, OutcomeOf(span));
        Assert.Equal(ActivityStatusCode.Unset, span.Status);
        var duration = Assert.Single(capture.DurationsFor(AddNoteOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.Success, duration.Tags[NexGradesTelemetry.OutcomeTag]);
        Assert.True(duration.Value >= 0);
    }

    [Fact]
    public async Task AddNoteAsync_RecordsClosedYearOutcome_AndRethrows_ForLockedClosedYear()
    {
        using var capture = new TelemetryCapture();
        using var env = new Env();
        var seed = await SeedAsync(env, SchoolYearStatus.Closed);

        await Assert.ThrowsAsync<ClosedYearException>(
            () => env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "text"));

        var span = Assert.Single(capture.SpansFor(AddNoteOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.ClosedYear, OutcomeOf(span));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(nameof(ClosedYearException), span.GetTagItem(NexGradesTelemetry.ErrorTypeTag));
        Assert.Equal(nameof(ClosedYearException), span.StatusDescription);
        var duration = Assert.Single(capture.DurationsFor(AddNoteOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.ClosedYear, duration.Tags[NexGradesTelemetry.OutcomeTag]);
        await using var verify = env.Db.CreateContext();
        Assert.Empty(verify.Notes);
    }

    [Fact]
    public async Task AddNoteAsync_RecordsInvariantViolationOutcome_AndRethrows_ForUnknownStudent()
    {
        using var capture = new TelemetryCapture();
        using var env = new Env();
        var seed = await SeedAsync(env);

        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.AddNoteAsync(9999, seed.YearId, "text"));

        var span = Assert.Single(capture.SpansFor(AddNoteOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.InvariantViolation, OutcomeOf(span));
        Assert.Equal(ActivityStatusCode.Error, span.Status);
        Assert.Equal(nameof(InvariantViolationException), span.GetTagItem(NexGradesTelemetry.ErrorTypeTag));
        Assert.Equal(nameof(InvariantViolationException), span.StatusDescription);
        var duration = Assert.Single(capture.DurationsFor(AddNoteOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.InvariantViolation, duration.Tags[NexGradesTelemetry.OutcomeTag]);
        await using var verify = env.Db.CreateContext();
        Assert.Empty(verify.Notes);
    }

    [Fact]
    public async Task AddNoteAsync_RecordsCancelledOutcome_ForCancelledToken()
    {
        using var capture = new TelemetryCapture();
        using var env = new Env();
        var seed = await SeedAsync(env);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => env.Service.AddNoteAsync(seed.StudentId, seed.YearId, "text", cancellationToken: cts.Token));

        var span = Assert.Single(capture.SpansFor(AddNoteOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.Cancelled, OutcomeOf(span));
        var duration = Assert.Single(capture.DurationsFor(AddNoteOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.Cancelled, duration.Tags[NexGradesTelemetry.OutcomeTag]);
        await using var verify = env.Db.CreateContext();
        Assert.Empty(verify.Notes);
    }

    // ---------- Privacy ----------

    [Fact]
    public async Task AddNoteAsync_EmitsNoRecordContent_InSpansOrMetrics()
    {
        const string SecretText = "ZZ-SECRET-NOTE-TEXT";
        const string SecretName = "Zzyzx Privatename";
        using var capture = new TelemetryCapture();
        using var env = new Env();
        var seed = await SeedAsync(env, firstName: SecretName);

        // One success and one failure whose exception message names ids, to cover both paths.
        await env.Service.AddNoteAsync(seed.StudentId, seed.YearId, SecretText, SecretName);
        await Assert.ThrowsAsync<InvariantViolationException>(
            () => env.Service.AddNoteAsync(seed.StudentId, 9999, SecretText, SecretName));

        var spans = capture.SpansFor(AddNoteOperation);
        Assert.Equal(2, spans.Count);
        var measurements = capture.DurationsFor(AddNoteOperation);
        Assert.Equal(2, measurements.Count);

        var allowed = NexGradesTelemetry.AllowedTagKeys;
        var strings = new List<string?>();
        foreach (var span in capture.AllSpans)
        {
            strings.Add(span.DisplayName);
            strings.Add(span.OperationName);
            strings.Add(span.StatusDescription);
            strings.Add(span.Source.Name);
            foreach (var tag in span.TagObjects)
            {
                Assert.Contains(tag.Key, allowed);
                strings.Add(tag.Value?.ToString());
            }

            foreach (var baggage in span.Baggage)
            {
                strings.Add(baggage.Key);
                strings.Add(baggage.Value);
            }

            foreach (var activityEvent in span.Events)
            {
                strings.Add(activityEvent.Name);
                foreach (var tag in activityEvent.Tags)
                {
                    strings.Add(tag.Key);
                    strings.Add(Convert.ToString(tag.Value, CultureInfo.InvariantCulture));
                }
            }
        }

        foreach (var measurement in capture.AllMeasurements)
        {
            foreach (var tag in measurement.Tags)
            {
                Assert.Contains(tag.Key, allowed);
                strings.Add(tag.Value?.ToString());
            }
        }

        Assert.DoesNotContain(strings, s => s is not null && s.Contains(SecretText, StringComparison.Ordinal));
        Assert.DoesNotContain(strings, s => s is not null && s.Contains(SecretName, StringComparison.Ordinal));
        Assert.DoesNotContain(strings, s => s is not null && s.Contains("Schmidt", StringComparison.Ordinal));
    }

    // ---------- Operation names of the catalogue services ----------

    private static async Task<int> SeedActiveClassAsync(Env env)
    {
        var seed = await SeedAsync(env);
        await using var context = env.Db.CreateContext();
        return await context.ClassInstances
            .Where(ci => ci.SchoolYearId == seed.YearId)
            .Select(ci => ci.Id)
            .SingleAsync();
    }

    [Fact]
    public async Task CreateClassAsync_RecordsOneSpan_WithFixedOperationName()
    {
        using var capture = new TelemetryCapture();
        using var env = new Env();
        await SeedAsync(env);

        await new ClassService(env.Db.CreateContextFactory()).CreateClassAsync("Test 7B");

        var span = Assert.Single(capture.SpansFor("ClassService.CreateClass"));
        Assert.Equal(NexGradesTelemetry.Outcomes.Success, OutcomeOf(span));
    }

    [Fact]
    public async Task RenameClassAsync_RecordsOneSpan_WithFixedOperationName()
    {
        using var capture = new TelemetryCapture();
        using var env = new Env();
        var classId = await SeedActiveClassAsync(env);

        await new ClassService(env.Db.CreateContextFactory()).RenameClassAsync(classId, "Test 7B");

        var span = Assert.Single(capture.SpansFor("ClassService.RenameClass"));
        Assert.Equal(NexGradesTelemetry.Outcomes.Success, OutcomeOf(span));
    }

    [Fact]
    public async Task CreateStudentAsync_RecordsOneSpan_WithFixedOperationName()
    {
        using var capture = new TelemetryCapture();
        using var env = new Env();
        var classId = await SeedActiveClassAsync(env);

        await new StudentService(env.Db.CreateContextFactory(), env.Clock).CreateStudentAsync("Test", "Person", classId);

        var span = Assert.Single(capture.SpansFor("StudentService.CreateStudent"));
        Assert.Equal(NexGradesTelemetry.Outcomes.Success, OutcomeOf(span));
    }

    [Fact]
    public async Task RenameStudentAsync_RecordsOneSpan_WithFixedOperationName()
    {
        using var capture = new TelemetryCapture();
        using var env = new Env();
        var seed = await SeedAsync(env);

        await new StudentService(env.Db.CreateContextFactory(), env.Clock)
            .RenameStudentAsync(seed.StudentId, "Other", "Name");

        var span = Assert.Single(capture.SpansFor("StudentService.RenameStudent"));
        Assert.Equal(NexGradesTelemetry.Outcomes.Success, OutcomeOf(span));
    }

    [Fact]
    public async Task CreateSubjectAsync_RecordsOneSpan_WithFixedOperationName()
    {
        using var capture = new TelemetryCapture();
        using var env = new Env();

        await new SubjectService(env.Db.CreateContextFactory()).CreateSubjectAsync("Test Subject", "TS");

        var span = Assert.Single(capture.SpansFor("SubjectService.CreateSubject"));
        Assert.Equal(NexGradesTelemetry.Outcomes.Success, OutcomeOf(span));
    }

    [Fact]
    public async Task CreateStudentAsync_EmitsNoNames_InSpansOrMetrics()
    {
        const string SecretFirst = "Zzyzx";
        const string SecretLast = "Privatename";
        using var capture = new TelemetryCapture();
        using var env = new Env();
        var classId = await SeedActiveClassAsync(env);
        var service = new StudentService(env.Db.CreateContextFactory(), env.Clock);

        await service.CreateStudentAsync(SecretFirst, SecretLast, classId);
        await Assert.ThrowsAsync<InvariantViolationException>(
            () => service.CreateStudentAsync(SecretFirst, SecretLast, 9999));

        Assert.Equal(2, capture.SpansFor("StudentService.CreateStudent").Count);
        var allowed = NexGradesTelemetry.AllowedTagKeys;
        var strings = new List<string?>();
        foreach (var span in capture.AllSpans)
        {
            strings.Add(span.DisplayName);
            strings.Add(span.OperationName);
            strings.Add(span.StatusDescription);
            foreach (var tag in span.TagObjects)
            {
                Assert.Contains(tag.Key, allowed);
                strings.Add(tag.Value?.ToString());
            }

            foreach (var activityEvent in span.Events)
            {
                strings.Add(activityEvent.Name);
                foreach (var tag in activityEvent.Tags)
                {
                    strings.Add(Convert.ToString(tag.Value, CultureInfo.InvariantCulture));
                }
            }
        }

        foreach (var measurement in capture.AllMeasurements)
        {
            foreach (var tag in measurement.Tags)
            {
                Assert.Contains(tag.Key, allowed);
                strings.Add(tag.Value?.ToString());
            }
        }

        Assert.DoesNotContain(strings, s => s is not null && s.Contains(SecretFirst, StringComparison.Ordinal));
        Assert.DoesNotContain(strings, s => s is not null && s.Contains(SecretLast, StringComparison.Ordinal));
    }

    // ---------- Startup migration ----------

    [Fact]
    public async Task RunAsync_RecordsSpanAndDuration_ForDatabaseMigration()
    {
        using var capture = new TelemetryCapture();
        using var db = new SqliteTestDatabase();
        var service = new DatabaseMigrationService(db.CreateContextFactory(), NullLogger<DatabaseMigrationService>.Instance);

        await service.RunAsync();

        var span = Assert.Single(capture.SpansFor(MigrationOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.Success, OutcomeOf(span));
        var duration = Assert.Single(capture.DurationsFor(MigrationOperation));
        Assert.Equal(NexGradesTelemetry.Outcomes.Success, duration.Tags[NexGradesTelemetry.OutcomeTag]);
        Assert.True(duration.Value >= 0);
    }
}
