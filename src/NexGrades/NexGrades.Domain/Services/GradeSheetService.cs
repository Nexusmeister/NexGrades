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

    /// <summary>
    /// Builds the grades of one student in one school year, per subject of the student's class and per bucket,
    /// with bucket and weighted subject averages. Read-only, so it also works on closed years. A student who is
    /// not enrolled in the year has no subjects. An exempt student's grades are listed but have no averages.
    /// </summary>
    /// <param name="studentId">The student.</param>
    /// <param name="schoolYearId">The school year to show.</param>
    /// <param name="includeVoided">
    /// Also return voided grades, flagged as voided, so they can be restored. They never count toward an
    /// average. When false, voided grades are neither returned nor counted.
    /// </param>
    /// <param name="cancellationToken">Cancels the load.</param>
    /// <exception cref="InvariantViolationException">The student or the school year does not exist.</exception>
    public Task<StudentGradeOverview> GetStudentGradesAsync(
        int studentId, int schoolYearId, bool includeVoided = false, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("GradeSheetService.GetStudentGrades", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);

            if (!await context.Students.AsNoTracking().AnyAsync(s => s.Id == studentId, cancellationToken))
            {
                throw new InvariantViolationException($"Student {studentId} does not exist.");
            }

            var yearLabel = await context.SchoolYears.AsNoTracking()
                .Where(y => y.Id == schoolYearId)
                .Select(y => y.Label)
                .FirstOrDefaultAsync(cancellationToken)
                ?? throw new InvariantViolationException($"School year {schoolYearId} does not exist.");

            // A student has at most one enrollment per school year.
            var enrollment = await context.Enrollments.AsNoTracking()
                .Where(e => e.StudentId == studentId && e.ClassInstance.SchoolYearId == schoolYearId)
                .Select(e => new { e.ClassInstanceId, e.JoinedOn, e.LeftOn })
                .FirstOrDefaultAsync(cancellationToken);
            if (enrollment is null)
            {
                return new StudentGradeOverview(studentId, schoolYearId, yearLabel, null, null, []);
            }

            var data = await LoadStudentClassDataAsync(context, studentId, enrollment.ClassInstanceId, includeVoided, cancellationToken);

            var subjects = data.SubjectInstances
                .Select(si => BuildSubject(si, data))
                .OrderBy(s => s.SubjectName, StringComparer.CurrentCulture)
                .ThenBy(s => s.SubjectInstanceId)
                .ToList();

            return new StudentGradeOverview(
                studentId,
                schoolYearId,
                yearLabel,
                DateOnly.FromDateTime(enrollment.JoinedOn),
                enrollment.LeftOn is { } left ? DateOnly.FromDateTime(left) : null,
                subjects);
        });

    private static async Task<StudentClassData> LoadStudentClassDataAsync(
        AppDbContext context, int studentId, int classInstanceId, bool includeVoided, CancellationToken cancellationToken)
    {
        var subjectInstances = await context.SubjectInstances.AsNoTracking()
            .Where(si => si.ClassInstanceId == classInstanceId)
            .Select(si => new SubjectInstanceRow(si.Id, si.Subject.Name))
            .ToListAsync(cancellationToken);

        var buckets = await context.GradeBuckets.AsNoTracking()
            .Where(b => b.SubjectInstance.ClassInstanceId == classInstanceId)
            .Select(b => new StudentBucketRow(b.SubjectInstanceId, new SubjectGradeData.BucketRow(b.Id, b.Name, b.Weight, b.SortOrder)))
            .ToListAsync(cancellationToken);

        var assessments = await context.Assessments.AsNoTracking()
            .Where(a => a.GradeBucket.SubjectInstance.ClassInstanceId == classInstanceId && a.VoidedOn == null)
            .Select(a => new GradeSheetAssessmentRow(a.GradeBucketId, new GradeSheetAssessment(a.Id, a.Title, a.Date, null)))
            .ToListAsync(cancellationToken);

        var exemptIds = (await context.SubjectParticipations.AsNoTracking()
            .Where(p => p.StudentId == studentId
                && p.SubjectInstance.ClassInstanceId == classInstanceId
                && p.Mode == Data.Entities.ParticipationMode.Exempt)
            .Select(p => p.SubjectInstanceId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var gradeQuery = context.Grades.AsNoTracking()
            .Where(g => g.StudentId == studentId && g.GradeBucket.SubjectInstance.ClassInstanceId == classInstanceId);
        if (!includeVoided)
        {
            gradeQuery = gradeQuery.Where(SubjectGradeData.CountingGrade);
        }

        var grades = await gradeQuery
            .Select(g => new SubjectGradeData.GradeRow(
                g.Id,
                g.StudentId,
                g.GradeBucketId,
                g.AssessmentId,
                g.Value,
                g.RecordedOn,
                g.Label,
                g.Comment,
                g.VoidedOn,
                g.Assessment == null ? null : g.Assessment.VoidedOn))
            .ToListAsync(cancellationToken);

        // Titles of every assessment the student has a grade in, voided ones included.
        var assessmentTitles = await context.Assessments.AsNoTracking()
            .Where(a => a.GradeBucket.SubjectInstance.ClassInstanceId == classInstanceId
                && a.Grades.Any(g => g.StudentId == studentId))
            .Select(a => new { a.Id, a.Title })
            .ToDictionaryAsync(a => a.Id, a => a.Title, cancellationToken);

        return new StudentClassData(subjectInstances, buckets, assessments, exemptIds, grades, assessmentTitles);
    }

    private static StudentSubjectGrades BuildSubject(SubjectInstanceRow subject, StudentClassData data)
    {
        var isExempt = data.ExemptSubjectInstanceIds.Contains(subject.Id);
        var sheetBuckets = data.Buckets
            .Where(b => b.SubjectInstanceId == subject.Id)
            .Select(b => b.Bucket)
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
            .Select(b => new GradeSheetBucket(
                b.Id,
                b.Name,
                b.Weight,
                b.SortOrder,
                [.. data.Assessments.Where(a => a.GradeBucketId == b.Id)
                    .Select(a => a.Assessment)
                    .OrderBy(a => a.Date).ThenBy(a => a.Id)]))
            .ToList();
        var bucketIds = sheetBuckets.Select(b => b.Id).ToHashSet();
        var subjectGrades = data.Grades.Where(g => bucketIds.Contains(g.GradeBucketId)).ToList();

        // Exempt wins over grades from before the exemption: listed, but no averages.
        var averages = isExempt
            ? new GradeSheetStudentAverages(sheetBuckets.ToDictionary(b => b.Id, _ => (decimal?)null), null)
            : GradeSheetAverages.Compute(
                sheetBuckets,
                subjectGrades.Select(g => new GradeSheetGrade(
                    g.Id, g.GradeBucketId, g.Value, TokenFor(g.Value), g.RecordedOn, g.Label, g.Comment, g.EffectiveVoidedOn)));

        var bucketResults = sheetBuckets
            .Select(b => new StudentBucketGrades(
                b.Id,
                b.Name,
                b.Weight,
                b.Assessments,
                averages.BucketAverages[b.Id],
                [.. subjectGrades
                    .Where(g => g.GradeBucketId == b.Id)
                    .OrderBy(g => g.RecordedOn).ThenBy(g => g.Id)
                    .Select(g => ToEntry(g, data.AssessmentTitles))]))
            .ToList();

        return new StudentSubjectGrades(subject.Id, subject.Name, isExempt, bucketResults, averages.SubjectAverage);
    }

    private static StudentGradeEntry ToEntry(SubjectGradeData.GradeRow g, Dictionary<int, string> assessmentTitles) =>
        new(
            g.Id,
            g.GradeBucketId,
            g.AssessmentId,
            g.AssessmentId is { } assessmentId ? assessmentTitles.GetValueOrDefault(assessmentId) : null,
            g.Value,
            TokenFor(g.Value),
            g.RecordedOn,
            g.Label,
            g.Comment,
            g.VoidedOn,
            g.AssessmentVoidedOn is not null);

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
                    : EnrollmentActivity.IsActiveOn(enrollment.JoinedOn, enrollment.LeftOn, column.Date)
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
            TokenFor(g.Value),
            g.RecordedOn,
            g.Label,
            g.Comment,
            g.EffectiveVoidedOn);

    // Legacy data may hold values outside the scale; show them rather than failing the view.
    private static string TokenFor(decimal value) =>
        GradingScale.TryGetTokenForValue(value, out var token) && token is not null
            ? token
            : value.ToString(CultureInfo.InvariantCulture);

    private sealed record GradeSheetAssessmentRow(int GradeBucketId, GradeSheetAssessment Assessment);

    private sealed record SubjectInstanceRow(int Id, string Name);

    private sealed record StudentBucketRow(int SubjectInstanceId, SubjectGradeData.BucketRow Bucket);

    private sealed record StudentClassData(
        List<SubjectInstanceRow> SubjectInstances,
        List<StudentBucketRow> Buckets,
        List<GradeSheetAssessmentRow> Assessments,
        HashSet<int> ExemptSubjectInstanceIds,
        List<SubjectGradeData.GradeRow> Grades,
        Dictionary<int, string> AssessmentTitles);
}
