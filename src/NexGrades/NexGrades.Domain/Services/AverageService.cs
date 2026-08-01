using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Domain.Grading;

namespace NexGrades.Domain.Services;

/// <summary>
/// Thin loading layer on top of the pure <see cref="AverageCalculator"/> functions, so callers (the UI) have
/// one call to make per average instead of hand-rolling the query every time. All computation is delegated
/// to <see cref="AverageCalculator"/>; this class only fetches the snapshot data it needs.
/// </summary>
public sealed class AverageService(AppDbContext context)
{
    /// <summary>Bucket average for one student in one <see cref="Data.Entities.GradeBucket"/>.</summary>
    public async Task<decimal?> GetBucketAverageAsync(int studentId, int gradeBucketId, CancellationToken cancellationToken = default)
    {
        var values = await context.Grades
            .Where(g => g.GradeBucketId == gradeBucketId && g.StudentId == studentId)
            .Select(g => g.Value)
            .ToListAsync(cancellationToken);

        return AverageCalculator.BucketAverage(values);
    }

    /// <summary>
    /// Subject average for one student in one <see cref="Data.Entities.SubjectInstance"/>, computed on read
    /// from the snapshotted <see cref="Data.Entities.GradeBucket.Weight"/> values of that instance.
    /// </summary>
    public async Task<decimal?> GetSubjectAverageAsync(int studentId, int subjectInstanceId, CancellationToken cancellationToken = default)
    {
        var buckets = await context.GradeBuckets
            .Where(b => b.SubjectInstanceId == subjectInstanceId)
            .Select(b => new
            {
                b.Weight,
                Values = b.Grades.Where(g => g.StudentId == studentId).Select(g => g.Value).ToList(),
            })
            .ToListAsync(cancellationToken);

        var weightedAverages = buckets.Select(b =>
            new AverageCalculator.WeightedBucketAverage(b.Weight, AverageCalculator.BucketAverage(b.Values)));

        return AverageCalculator.SubjectAverage(weightedAverages);
    }
}
