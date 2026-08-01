namespace NexGrades.Domain.Grading;

/// <summary>
/// Pure, testable average computation. Takes already-loaded data rather than querying itself, so it can be
/// unit tested without a database. Always uses <see cref="decimal"/> — never <see cref="double"/>/<see cref="float"/> —
/// so results are exact and reproducible.
/// </summary>
public static class AverageCalculator
{
    /// <summary>
    /// A bucket's snapshotted weight together with that student's average within it (or <see langword="null"/>
    /// if the student has no grades in that bucket).
    /// </summary>
    public readonly record struct WeightedBucketAverage(decimal Weight, decimal? Average);

    /// <summary>
    /// Bucket average = plain arithmetic mean of the grade values in that bucket for one student.
    /// A bucket with zero grades has no average: returns <see langword="null"/>, not zero.
    /// </summary>
    public static decimal? BucketAverage(IEnumerable<decimal> gradeValues)
    {
        var values = gradeValues as IReadOnlyCollection<decimal> ?? gradeValues.ToList();
        return values.Count == 0 ? null : values.Average();
    }

    /// <summary>
    /// Subject average = Σ(bucketAvg × bucket.Weight) ÷ Σ(bucket.Weight), summed only over buckets that
    /// contain at least one grade for the student (i.e. whose <see cref="WeightedBucketAverage.Average"/> is
    /// not <see langword="null"/>). If no bucket has any grades, there is no subject average:
    /// returns <see langword="null"/>. Must be called with the snapshotted <c>GradeBucket.Weight</c>, never
    /// the live <c>BucketTemplate.Weight</c> — the whole point of the snapshot is that it doesn't drift.
    /// </summary>
    public static decimal? SubjectAverage(IEnumerable<WeightedBucketAverage> buckets)
    {
        decimal weightedSum = 0m;
        decimal weightSum = 0m;

        foreach (var bucket in buckets)
        {
            if (bucket.Average is not { } average)
            {
                continue;
            }

            weightedSum += average * bucket.Weight;
            weightSum += bucket.Weight;
        }

        return weightSum == 0m ? null : weightedSum / weightSum;
    }
}
