namespace NexGrades.Domain.Grading;

/// <summary>
/// Pure average computation for one student row on the grade sheet. Both <c>GradeSheetService</c> and the
/// view model use it, so a recomputed row after an edit always agrees with a freshly loaded sheet. It wraps
/// <see cref="AverageCalculator"/>, so weights are normalised over the buckets that have grades.
/// </summary>
public static class GradeSheetAverages
{
    /// <summary>
    /// Computes the bucket and subject averages of one student. Uses the snapshotted
    /// <see cref="GradeSheetBucket.Weight"/> and skips voided grades.
    /// </summary>
    /// <param name="buckets">The sheet buckets.</param>
    /// <param name="grades">All grades of the student on the sheet; voided ones are ignored.</param>
    public static GradeSheetStudentAverages Compute(IEnumerable<GradeSheetBucket> buckets, IEnumerable<GradeSheetGrade> grades)
    {
        ArgumentNullException.ThrowIfNull(buckets);
        ArgumentNullException.ThrowIfNull(grades);

        var counting = grades.Where(g => g.VoidedOn is null).ToLookup(g => g.GradeBucketId);
        var bucketAverages = new Dictionary<int, decimal?>();
        var weighted = new List<AverageCalculator.WeightedBucketAverage>();

        foreach (var bucket in buckets)
        {
            var average = AverageCalculator.BucketAverage(counting[bucket.Id].Select(g => g.Value));
            bucketAverages[bucket.Id] = average;
            weighted.Add(new AverageCalculator.WeightedBucketAverage(bucket.Weight, average));
        }

        return new GradeSheetStudentAverages(bucketAverages, AverageCalculator.SubjectAverage(weighted));
    }
}
