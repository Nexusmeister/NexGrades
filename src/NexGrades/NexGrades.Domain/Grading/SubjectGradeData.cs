using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Grading;

/// <summary>
/// The raw data of one subject instance (header, buckets, enrollments, exempt students and grades), loaded
/// the same way for the grade sheet and the class overview export. The "voided grades and grades of voided
/// assessments do not count" rule is applied here, in one place, unless <c>includeVoided</c> is set.
/// </summary>
internal sealed class SubjectGradeData
{
    private SubjectGradeData()
    {
    }

    /// <summary>Header values of the subject instance and its school year.</summary>
    public required HeaderRow Header { get; init; }

    /// <summary>The buckets ordered by sort order, then id.</summary>
    public required IReadOnlyList<BucketRow> Buckets { get; init; }

    /// <summary>All enrollments of the class, including students who left.</summary>
    public required IReadOnlyList<EnrollmentRow> Enrollments { get; init; }

    /// <summary>Ids of the students exempt from the subject instance.</summary>
    public required IReadOnlySet<int> ExemptStudentIds { get; init; }

    /// <summary>The grades of the subject instance, without voided ones unless they were asked for.</summary>
    public required IReadOnlyList<GradeRow> Grades { get; init; }

    /// <summary>
    /// The one definition of "this grade counts": neither the grade nor its assessment is voided. Shared with the
    /// per-student grade list, so the two views cannot drift apart.
    /// </summary>
    public static Expression<Func<Grade, bool>> CountingGrade { get; } =
        g => g.VoidedOn == null && (g.Assessment == null || g.Assessment.VoidedOn == null);

    /// <summary>Loads the data on <paramref name="context"/> with no tracking.</summary>
    /// <exception cref="InvariantViolationException">The subject instance does not exist.</exception>
    public static async Task<SubjectGradeData> LoadAsync(
        AppDbContext context, int subjectInstanceId, bool includeVoided, CancellationToken cancellationToken)
    {
        var header = await context.SubjectInstances.AsNoTracking()
            .Where(si => si.Id == subjectInstanceId)
            .Select(si => new HeaderRow(
                si.Subject.Name,
                si.ClassInstanceId,
                si.ClassInstance.Name,
                si.ClassInstance.SchoolYearId,
                si.ClassInstance.SchoolYear.Label,
                si.ClassInstance.SchoolYear.Status,
                si.ClassInstance.SchoolYear.UnlockedUntil))
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new InvariantViolationException($"Subject instance {subjectInstanceId} does not exist.");

        var buckets = (await context.GradeBuckets.AsNoTracking()
            .Where(b => b.SubjectInstanceId == subjectInstanceId)
            .Select(b => new BucketRow(b.Id, b.Name, b.Weight, b.SortOrder))
            .ToListAsync(cancellationToken))
            .OrderBy(b => b.SortOrder).ThenBy(b => b.Id)
            .ToList();

        var enrollments = await context.Enrollments.AsNoTracking()
            .Where(e => e.ClassInstanceId == header.ClassInstanceId)
            .Select(e => new EnrollmentRow(e.StudentId, e.Student.FirstName, e.Student.LastName, e.JoinedOn, e.LeftOn))
            .ToListAsync(cancellationToken);

        var exemptIds = (await context.SubjectParticipations.AsNoTracking()
            .Where(p => p.SubjectInstanceId == subjectInstanceId && p.Mode == ParticipationMode.Exempt)
            .Select(p => p.StudentId)
            .ToListAsync(cancellationToken))
            .ToHashSet();

        var grades = await LoadGradesAsync(context, subjectInstanceId, includeVoided, cancellationToken);

        return new SubjectGradeData
        {
            Header = header,
            Buckets = buckets,
            Enrollments = enrollments,
            ExemptStudentIds = exemptIds,
            Grades = grades,
        };
    }

    private static Task<List<GradeRow>> LoadGradesAsync(
        AppDbContext context, int subjectInstanceId, bool includeVoided, CancellationToken cancellationToken)
    {
        var gradeQuery = context.Grades.AsNoTracking()
            .Where(g => g.GradeBucket.SubjectInstanceId == subjectInstanceId);
        if (!includeVoided)
        {
            gradeQuery = gradeQuery.Where(CountingGrade);
        }

        return gradeQuery
            .Select(g => new GradeRow(
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
    }

    /// <summary>Header values of the subject instance.</summary>
    public sealed record HeaderRow(
        string SubjectName,
        int ClassInstanceId,
        string ClassName,
        int SchoolYearId,
        string SchoolYearLabel,
        SchoolYearStatus SchoolYearStatus,
        DateTime? UnlockedUntil);

    /// <summary>A bucket with its snapshotted weight.</summary>
    public sealed record BucketRow(int Id, string Name, decimal Weight, int SortOrder);

    /// <summary>A student enrollment in the class.</summary>
    public sealed record EnrollmentRow(int StudentId, string FirstName, string LastName, DateTime JoinedOn, DateTime? LeftOn);

    /// <summary>A grade; <see cref="EffectiveVoidedOn"/> folds in the voided state of its assessment.</summary>
    public sealed record GradeRow(
        int Id,
        int StudentId,
        int GradeBucketId,
        int? AssessmentId,
        decimal Value,
        DateOnly RecordedOn,
        string? Label,
        string? Comment,
        DateTime? VoidedOn,
        DateTime? AssessmentVoidedOn)
    {
        /// <summary>The grade own voided time, or that of its assessment; null while the grade counts.</summary>
        public DateTime? EffectiveVoidedOn => VoidedOn ?? AssessmentVoidedOn;
    }
}
