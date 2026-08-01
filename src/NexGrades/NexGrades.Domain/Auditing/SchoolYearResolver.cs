using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;

namespace NexGrades.Domain.Auditing;

/// <summary>
/// Resolves which <see cref="SchoolYear"/> (if any) a changed entity is reachable from, by walking the known
/// traversal paths:
/// <list type="bullet">
/// <item><c>Grade</c> → <c>GradeBucket</c> → <c>SubjectInstance</c> → <c>ClassInstance</c> → <c>SchoolYear</c></item>
/// <item><c>GradeBucket</c> → <c>SubjectInstance</c> → <c>ClassInstance</c> → <c>SchoolYear</c></item>
/// <item><c>SubjectInstance</c> → <c>ClassInstance</c> → <c>SchoolYear</c></item>
/// <item><c>SubjectParticipation</c> → <c>SubjectInstance</c> → <c>ClassInstance</c> → <c>SchoolYear</c></item>
/// <item><c>Enrollment</c> → <c>ClassInstance</c> → <c>SchoolYear</c></item>
/// <item><c>Note</c> → <c>SchoolYearId</c> directly</item>
/// <item><c>ClassInstance</c> → <c>SchoolYearId</c> directly</item>
/// </list>
/// Everything else (<c>Student</c>, <c>Subject</c>, <c>BucketTemplate</c>, <c>Teacher</c>, <c>ClassGroup</c>,
/// <c>SchoolYear</c> itself, <c>AuditEntry</c> itself) has no year association and resolves to
/// <see langword="null"/> — out of scope for the closed-year lock and never audited by this mechanism.
/// Each hop uses <see cref="DbContext.Find{TEntity}"/>/<see cref="DbContext.FindAsync{TEntity}(object[])"/>,
/// which check the change tracker's local cache before querying the database, so entities created earlier in
/// the same <c>SaveChanges</c> call (and already persisted via an intermediate save, as
/// <c>RolloverService</c> does) resolve correctly without a wasted round trip.
/// </summary>
internal static class SchoolYearResolver
{
    /// <summary>Synchronous resolution, for use from <c>SavingChanges</c>.</summary>
    public static int? Resolve(DbContext context, object entity) => entity switch
    {
        Note note => note.SchoolYearId,
        ClassInstance classInstance => classInstance.SchoolYearId,
        Grade grade => FromGradeBucket(context, grade.GradeBucketId),
        GradeBucket bucket => FromSubjectInstance(context, bucket.SubjectInstanceId),
        SubjectInstance subjectInstance => FromClassInstance(context, subjectInstance.ClassInstanceId),
        SubjectParticipation participation => FromSubjectInstance(context, participation.SubjectInstanceId),
        Enrollment enrollment => FromClassInstance(context, enrollment.ClassInstanceId),
        _ => null,
    };

    /// <summary>Asynchronous resolution, for use from <c>SavingChangesAsync</c>.</summary>
    public static Task<int?> ResolveAsync(DbContext context, object entity, CancellationToken cancellationToken) => entity switch
    {
        Note note => Task.FromResult<int?>(note.SchoolYearId),
        ClassInstance classInstance => Task.FromResult<int?>(classInstance.SchoolYearId),
        Grade grade => FromGradeBucketAsync(context, grade.GradeBucketId, cancellationToken),
        GradeBucket bucket => FromSubjectInstanceAsync(context, bucket.SubjectInstanceId, cancellationToken),
        SubjectInstance subjectInstance => FromClassInstanceAsync(context, subjectInstance.ClassInstanceId, cancellationToken),
        SubjectParticipation participation => FromSubjectInstanceAsync(context, participation.SubjectInstanceId, cancellationToken),
        Enrollment enrollment => FromClassInstanceAsync(context, enrollment.ClassInstanceId, cancellationToken),
        _ => Task.FromResult<int?>(null),
    };

    private static int? FromGradeBucket(DbContext context, int gradeBucketId)
    {
        var bucket = context.Find<GradeBucket>(gradeBucketId);
        return bucket is null ? null : FromSubjectInstance(context, bucket.SubjectInstanceId);
    }

    private static int? FromSubjectInstance(DbContext context, int subjectInstanceId)
    {
        var subjectInstance = context.Find<SubjectInstance>(subjectInstanceId);
        return subjectInstance is null ? null : FromClassInstance(context, subjectInstance.ClassInstanceId);
    }

    private static int? FromClassInstance(DbContext context, int classInstanceId)
    {
        var classInstance = context.Find<ClassInstance>(classInstanceId);
        return classInstance?.SchoolYearId;
    }

    private static async Task<int?> FromGradeBucketAsync(DbContext context, int gradeBucketId, CancellationToken cancellationToken)
    {
        var bucket = await context.FindAsync<GradeBucket>([gradeBucketId], cancellationToken);
        return bucket is null ? null : await FromSubjectInstanceAsync(context, bucket.SubjectInstanceId, cancellationToken);
    }

    private static async Task<int?> FromSubjectInstanceAsync(DbContext context, int subjectInstanceId, CancellationToken cancellationToken)
    {
        var subjectInstance = await context.FindAsync<SubjectInstance>([subjectInstanceId], cancellationToken);
        return subjectInstance is null ? null : await FromClassInstanceAsync(context, subjectInstance.ClassInstanceId, cancellationToken);
    }

    private static async Task<int?> FromClassInstanceAsync(DbContext context, int classInstanceId, CancellationToken cancellationToken)
    {
        var classInstance = await context.FindAsync<ClassInstance>([classInstanceId], cancellationToken);
        return classInstance?.SchoolYearId;
    }
}
