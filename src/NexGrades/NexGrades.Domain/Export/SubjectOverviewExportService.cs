using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;
using NexGrades.Domain.Services;

namespace NexGrades.Domain.Export;

/// <summary>
/// Builds the class overview of one subject instance: a row per student with the grades and average of each
/// bucket, the weighted subject average and the final grade (the teacher's override, otherwise the proposal). Read-only, so it also works on closed years.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <remarks>
/// Averages come from <see cref="AverageCalculator"/> with the snapshotted <see cref="Data.Entities.GradeBucket.Weight"/>.
/// Exempt students are omitted even if they have grades (exempt wins). Students who left the class appear
/// only if they have grades in this subject.
/// </remarks>
public sealed class SubjectOverviewExportService(IDbContextFactory<AppDbContext> contextFactory)
{
    private const string GradeSeparator = "; ";
    private const string OverriddenMarker = "Yes";

    /// <summary>Builds the overview table for <paramref name="subjectInstanceId"/>.</summary>
    /// <exception cref="InvariantViolationException">The subject instance does not exist.</exception>
    public Task<SubjectOverviewTable> BuildAsync(int subjectInstanceId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SubjectOverviewExportService.Build", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var data = await SubjectGradeData.LoadAsync(context, subjectInstanceId, includeVoided: false, cancellationToken);
            var header = data.Header;
            var buckets = data.Buckets;
            var enrollments = data.Enrollments;
            var exemptIds = data.ExemptStudentIds;
            var grades = data.Grades;

            var gradesByStudent = grades.ToLookup(g => g.StudentId);

            var finalGradeOverrides = await context.FinalGrades.AsNoTracking()
                .Where(f => f.SubjectInstanceId == subjectInstanceId && f.OverrideValue != null)
                .Select(f => new { f.StudentId, f.OverrideValue })
                .ToDictionaryAsync(f => f.StudentId, f => f.OverrideValue, cancellationToken);

            var included = enrollments
                .Where(e => !exemptIds.Contains(e.StudentId))
                .Where(e => e.LeftOn is null || gradesByStudent[e.StudentId].Any())
                .ToList();

            var rows = Sort(included.Where(e => e.LeftOn is null))
                .Concat(Sort(included.Where(e => e.LeftOn is not null)))
                .Select(e => BuildRow(e, buckets, [.. gradesByStudent[e.StudentId]], finalGradeOverrides.GetValueOrDefault(e.StudentId)))
                .ToList();

            return new SubjectOverviewTable(header.SubjectName, header.ClassName, header.SchoolYearLabel, BuildColumns(buckets), rows);
        });

    private static List<SubjectOverviewColumn> BuildColumns(IReadOnlyList<SubjectGradeData.BucketRow> buckets)
    {
        var columns = new List<SubjectOverviewColumn> { new(SubjectOverviewColumnKind.Student, null) };
        foreach (var bucket in buckets)
        {
            columns.Add(new(SubjectOverviewColumnKind.BucketGrades, bucket.Name));
            columns.Add(new(SubjectOverviewColumnKind.BucketAverage, bucket.Name));
        }

        columns.Add(new(SubjectOverviewColumnKind.SubjectAverage, null));
        columns.Add(new(SubjectOverviewColumnKind.FinalGrade, null));
        columns.Add(new(SubjectOverviewColumnKind.FinalGradeOverridden, null));
        return columns;
    }

    private static IEnumerable<SubjectGradeData.EnrollmentRow> Sort(IEnumerable<SubjectGradeData.EnrollmentRow> enrollments) =>
        enrollments
            .OrderBy(e => e.LastName, StringComparer.CurrentCulture)
            .ThenBy(e => e.FirstName, StringComparer.CurrentCulture)
            .ThenBy(e => e.StudentId);

    private static SubjectOverviewRow BuildRow(
        SubjectGradeData.EnrollmentRow student,
        IReadOnlyList<SubjectGradeData.BucketRow> buckets,
        List<SubjectGradeData.GradeRow> studentGrades,
        decimal? finalGradeOverride)
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

        var subjectAverage = AverageCalculator.SubjectAverage(weighted);
        cells.Add(SubjectOverviewCell.FromNumber(subjectAverage));

        // The proposal is derived here, never stored; no override and no average leaves the cell empty, not 0.
        cells.Add(SubjectOverviewCell.FromNumber(finalGradeOverride ?? GradingScale.ProposeFinalGrade(subjectAverage)));
        cells.Add(finalGradeOverride is null ? SubjectOverviewCell.Empty : SubjectOverviewCell.FromText(OverriddenMarker));

        DateOnly? leftOn = student.LeftOn is { } left ? DateOnly.FromDateTime(left) : null;
        return new SubjectOverviewRow(cells, leftOn);
    }

    // Legacy data may hold values outside the scale; show them rather than failing the export.
    private static string ToToken(decimal value) =>
        GradingScale.TryGetTokenForValue(value, out var token) && token is not null
            ? token
            : value.ToString(CultureInfo.InvariantCulture);
}
