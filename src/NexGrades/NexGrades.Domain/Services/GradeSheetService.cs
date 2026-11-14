using System.Globalization;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Grading;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Builds the grade sheet of one subject instance: a row per student, a column per assessment grouped by
/// bucket, and the live bucket and subject averages. Read-only, so it also works on closed years.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <param name="clock">Source of "now", used to tell whether the year can currently be written to.</param>
/// <remarks>
/// Averages come from <see cref="GradeSheetAverages"/> with the snapshotted bucket weights. Every student of
/// the class is shown, including those who left, so grades from before they left can still be entered; a cell
/// on a date when the student was not enrolled is <see cref="GradeSheetCellState.NotEnrolled"/>. Exempt wins:
/// an exempt student has only <see cref="GradeSheetCellState.Exempt"/> cells and no averages.
/// </remarks>
public sealed class GradeSheetService(IDbContextFactory<AppDbContext> contextFactory, IClock clock)
{
    /// <summary>Builds the grade sheet for <paramref name="subjectInstanceId"/>.</summary>
    /// <param name="subjectInstanceId">The subject instance to show.</param>
    /// <param name="includeVoided">
    /// Also show voided grades and voided assessments, flagged through their voided time, so they can be
    /// restored. They never count toward an average.
    /// </param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <exception cref="InvariantViolationException">The subject instance does not exist.</exception>
    public Task<GradeSheet> GetGradeSheetAsync(
        int subjectInstanceId, bool includeVoided = false, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("GradeSheetService.GetGradeSheet", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            var data = await SubjectGradeData.LoadAsync(context, subjectInstanceId, includeVoided, cancellationToken);

            var assessmentQuery = context.Assessments.AsNoTracking()
                .Where(a => a.GradeBucket.SubjectInstanceId == subjectInstanceId);
            if (!includeVoided)
            {
                assessmentQuery = assessmentQuery.Where(a => a.VoidedOn == null);
            }

            var assessments = await assessmentQuery
                .Select(a => new GradeSheetAssessmentRow(a.GradeBucketId, new GradeSheetAssessment(a.Id, a.Title, a.Date, a.VoidedOn)))
                .ToListAsync(cancellationToken);

            var buckets = data.Buckets
                .Select(b => new GradeSheetBucket(
                    b.Id,
                    b.Name,
                    b.Weight,
                    b.SortOrder,
                    [.. assessments.Where(a => a.GradeBucketId == b.Id)
                        .Select(a => a.Assessment)
                        .OrderBy(a => a.Date).ThenBy(a => a.Id)]))
                .ToList();
            var columns = buckets.SelectMany(b => b.Assessments).ToList();

            var gradesByStudent = data.Grades.ToLookup(g => g.StudentId);
            var students = Sort(data.Enrollments.Where(e => e.LeftOn is null))
                .Concat(Sort(data.Enrollments.Where(e => e.LeftOn is not null)))
                .Select(e => BuildStudent(e, data.ExemptStudentIds.Contains(e.StudentId), buckets, columns, [.. gradesByStudent[e.StudentId]]))
                .ToList();

            var isClosed = data.Header.SchoolYearStatus == Data.Entities.SchoolYearStatus.Closed;
            return new GradeSheet(
                subjectInstanceId,
                data.Header.SubjectName,
                data.Header.ClassName,
                data.Header.SchoolYearId,
                data.Header.SchoolYearLabel,
                SchoolYearLock.IsWritable(data.Header.SchoolYearStatus, data.Header.UnlockedUntil, clock.Now),
                isClosed,
                data.Header.UnlockedUntil,
                buckets,
                students);
        });

    private static IEnumerable<SubjectGradeData.EnrollmentRow> Sort(IEnumerable<SubjectGradeData.EnrollmentRow> enrollments) =>
        enrollments
            .OrderBy(e => e.LastName, StringComparer.CurrentCulture)
            .ThenBy(e => e.FirstName, StringComparer.CurrentCulture)
            .ThenBy(e => e.StudentId);

    private static GradeSheetStudent BuildStudent(
        SubjectGradeData.EnrollmentRow enrollment,
        bool isExempt,
        List<GradeSheetBucket> buckets,
        List<GradeSheetAssessment> columns,
        List<SubjectGradeData.GradeRow> studentGrades)
    {
        var grades = studentGrades.Select(ToGrade).ToList();

        var cells = columns
            .Select(column =>
            {
                var state = isExempt
                    ? GradeSheetCellState.Exempt
                    : EnrollmentActivity.IsActive(enrollment.JoinedOn, enrollment.LeftOn, column.Date.ToDateTime(TimeOnly.MinValue))
                        ? GradeSheetCellState.Editable
                        : GradeSheetCellState.NotEnrolled;

                // An active grade wins; otherwise show the latest voided one so it can be restored.
                var grade = studentGrades
                    .Where(g => g.AssessmentId == column.Id)
                    .OrderBy(g => g.EffectiveVoidedOn is null ? 0 : 1)
                    .ThenByDescending(g => g.Id)
                    .Select(ToGrade)
                    .FirstOrDefault();
                return new GradeSheetCell(column.Id, state, grade);
            })
            .ToList();

        var unassigned = studentGrades
            .Where(g => g.AssessmentId is null)
            .OrderBy(g => g.RecordedOn).ThenBy(g => g.Id)
            .Select(ToGrade)
            .ToList();

        // Exempt wins over grades the student has from before the exemption: no averages, as in the export.
        var averages = isExempt
            ? new GradeSheetStudentAverages(buckets.ToDictionary(b => b.Id, _ => (decimal?)null), null)
            : GradeSheetAverages.Compute(buckets, grades);

        return new GradeSheetStudent(
            enrollment.StudentId,
            enrollment.FirstName,
            enrollment.LastName,
            DateOnly.FromDateTime(enrollment.JoinedOn),
            enrollment.LeftOn is { } left ? DateOnly.FromDateTime(left) : null,
            isExempt,
            cells,
            unassigned,
            averages.BucketAverages,
            averages.SubjectAverage);
    }

    private static GradeSheetGrade ToGrade(SubjectGradeData.GradeRow g) =>
        new(
            g.Id,
            g.GradeBucketId,
            g.Value,
            // Legacy data may hold values outside the scale; show them rather than failing the sheet.
            GradingScale.TryGetTokenForValue(g.Value, out var token) && token is not null
                ? token
                : g.Value.ToString(CultureInfo.InvariantCulture),
            g.RecordedOn,
            g.Label,
            g.Comment,
            g.EffectiveVoidedOn);

    private sealed record GradeSheetAssessmentRow(int GradeBucketId, GradeSheetAssessment Assessment);
}
