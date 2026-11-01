using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;
using NexGrades.Domain.Services;

namespace NexGrades.Domain.Export;

/// <summary>
/// Builds the class overview of one subject instance: a row per student with the grades and average of each
/// bucket and the weighted subject average. Read-only, so it also works on closed years.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <remarks>
/// Averages come from <see cref="AverageCalculator"/> with the snapshotted <see cref="GradeBucket.Weight"/>.
/// Exempt students are omitted even if they have grades (exempt wins). Students who left the class appear
/// only if they have grades in this subject.
/// </remarks>
public sealed class SubjectOverviewExportService(IDbContextFactory<AppDbContext> contextFactory)
{
    private const string GradeSeparator = "; ";

    /// <summary>Builds the overview table for <paramref name="subjectInstanceId"/>.</summary>
    /// <exception cref="InvariantViolationException">The subject instance does not exist.</exception>
    public Task<SubjectOverviewTable> BuildAsync(int subjectInstanceId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SubjectOverviewExportService.Build", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var header = await context.SubjectInstances.AsNoTracking()
                .Where(si => si.Id == subjectInstanceId)
                .Select(si => new
                {
                    SubjectName = si.Subject.Name,
                    si.ClassInstanceId,
                    ClassName = si.ClassInstance.Name,
                    SchoolYearLabel = si.ClassInstance.SchoolYear.Label,
                })
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvariantViolationException($"Subject instance {subjectInstanceId} does not exist.");

            var buckets = (await context.GradeBuckets.AsNoTracking()
                .Where(b => b.SubjectInstanceId == subjectInstanceId)
                .Select(b => new BucketInfo(b.Id, b.Name, b.Weight, b.SortOrder))
                .ToListAsync(cancellationToken))
                .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
                .ToList();

            var enrollments = await context.Enrollments.AsNoTracking()
                .Where(e => e.ClassInstanceId == header.ClassInstanceId)
                .Select(e => new EnrollmentInfo(e.StudentId, e.Student.FirstName, e.Student.LastName, e.LeftOn))
                .ToListAsync(cancellationToken);

            var exemptIds = (await context.SubjectParticipations.AsNoTracking()
                .Where(p => p.SubjectInstanceId == subjectInstanceId && p.Mode == ParticipationMode.Exempt)
                .Select(p => p.StudentId)
                .ToListAsync(cancellationToken))
                .ToHashSet();

            var grades = await context.Grades.AsNoTracking()
                .Where(g => g.GradeBucket.SubjectInstanceId == subjectInstanceId)
                .Select(g => new GradeInfo(g.StudentId, g.GradeBucketId, g.Value, g.RecordedOn, g.Id))
                .ToListAsync(cancellationToken);

            var gradesByStudent = grades.ToLookup(g => g.StudentId);

            var included = enrollments
                .Where(e => !exemptIds.Contains(e.StudentId))
                .Where(e => e.LeftOn is null || gradesByStudent[e.StudentId].Any())
                .ToList();

            var rows = Sort(included.Where(e => e.LeftOn is null))
                .Concat(Sort(included.Where(e => e.LeftOn is not null)))
                .Select(e => BuildRow(e, buckets, [.. gradesByStudent[e.StudentId]]))
                .ToList();

            return new SubjectOverviewTable(header.SubjectName, header.ClassName, header.SchoolYearLabel, BuildColumns(buckets), rows);
        });

    private static List<SubjectOverviewColumn> BuildColumns(List<BucketInfo> buckets)
    {
        var columns = new List<SubjectOverviewColumn> { new(SubjectOverviewColumnKind.Student, null) };
        foreach (var bucket in buckets)
        {
            columns.Add(new(SubjectOverviewColumnKind.BucketGrades, bucket.Name));
            columns.Add(new(SubjectOverviewColumnKind.BucketAverage, bucket.Name));
        }

        columns.Add(new(SubjectOverviewColumnKind.SubjectAverage, null));
        return columns;
    }

    private static IEnumerable<EnrollmentInfo> Sort(IEnumerable<EnrollmentInfo> enrollments) =>
        enrollments
            .OrderBy(e => e.LastName, StringComparer.CurrentCulture)
            .ThenBy(e => e.FirstName, StringComparer.CurrentCulture)
            .ThenBy(e => e.StudentId);

    private static SubjectOverviewRow BuildRow(EnrollmentInfo student, List<BucketInfo> buckets, List<GradeInfo> studentGrades)
    {
        var cells = new List<SubjectOverviewCell>
        {
            SubjectOverviewCell.FromText($"{student.LastName}, {student.FirstName}"),
        };
        var weighted = new List<AverageCalculator.WeightedBucketAverage>();

        foreach (var bucket in buckets)
        {
            var bucketGrades = studentGrades
                .Where(g => g.GradeBucketId == bucket.Id)
                .OrderBy(g => g.RecordedOn).ThenBy(g => g.Id)
                .ToList();

            cells.Add(bucketGrades.Count == 0
                ? SubjectOverviewCell.Empty
                : SubjectOverviewCell.FromText(string.Join(GradeSeparator, bucketGrades.Select(g => ToToken(g.Value)))));

            var average = AverageCalculator.BucketAverage(bucketGrades.Select(g => g.Value));
            cells.Add(SubjectOverviewCell.FromNumber(average));
            weighted.Add(new AverageCalculator.WeightedBucketAverage(bucket.Weight, average));
        }

        cells.Add(SubjectOverviewCell.FromNumber(AverageCalculator.SubjectAverage(weighted)));

        DateOnly? leftOn = student.LeftOn is { } left ? DateOnly.FromDateTime(left) : null;
        return new SubjectOverviewRow(cells, leftOn);
    }

    // Legacy data may hold values outside the scale; show them rather than failing the export.
    private static string ToToken(decimal value) =>
        GradingScale.TryGetTokenForValue(value, out var token) && token is not null
            ? token
            : value.ToString(CultureInfo.InvariantCulture);

    private sealed record BucketInfo(int Id, string Name, decimal Weight, int SortOrder);

    private sealed record EnrollmentInfo(int StudentId, string FirstName, string LastName, DateTime? LeftOn);

    private sealed record GradeInfo(int StudentId, int GradeBucketId, decimal Value, DateOnly RecordedOn, int Id);
}
