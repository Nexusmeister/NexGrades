using NexGrades.Domain.Grading;

namespace NexGrades.Tests;

public class GradeSheetAveragesTests
{
    private static GradeSheetBucket Bucket(int id, decimal weight) => new(id, $"Bucket {id}", weight, id, []);

    private static GradeSheetGrade Grade(int id, int bucketId, decimal value, DateTime? voidedOn = null) =>
        new(id, bucketId, value, "x", new DateOnly(2025, 9, 15), null, null, voidedOn);

    [Fact]
    public void Compute_ReturnsNullAverages_ForNoGrades()
    {
        var result = GradeSheetAverages.Compute([Bucket(1, 100m)], []);

        Assert.Null(result.BucketAverages[1]);
        Assert.Null(result.SubjectAverage);
    }

    [Fact]
    public void Compute_ReturnsNoBuckets_AndNullSubjectAverage_ForNoBuckets()
    {
        var result = GradeSheetAverages.Compute([], [Grade(1, 1, 2.0m)]);

        Assert.Empty(result.BucketAverages);
        Assert.Null(result.SubjectAverage);
    }

    [Fact]
    public void Compute_AveragesGradesPerBucket()
    {
        var result = GradeSheetAverages.Compute(
            [Bucket(1, 50m), Bucket(2, 50m)],
            [Grade(1, 1, 2.0m), Grade(2, 1, 3.0m), Grade(3, 2, 1.0m)]);

        Assert.Equal(2.5m, result.BucketAverages[1]);
        Assert.Equal(1.0m, result.BucketAverages[2]);
    }

    [Fact]
    public void Compute_WeightsBucketAverages_BySnapshottedWeight()
    {
        var result = GradeSheetAverages.Compute(
            [Bucket(1, 75m), Bucket(2, 25m)],
            [Grade(1, 1, 2.0m), Grade(2, 2, 4.0m)]);

        Assert.Equal(2.5m, result.SubjectAverage);
    }

    [Fact]
    public void Compute_NormalisesWeights_ThatDoNotSumToHundred()
    {
        var result = GradeSheetAverages.Compute(
            [Bucket(1, 30m), Bucket(2, 30m)],
            [Grade(1, 1, 2.0m), Grade(2, 2, 4.0m)]);

        Assert.Equal(3.0m, result.SubjectAverage);
    }

    [Fact]
    public void Compute_LeavesAnEmptyBucketOutOfTheSubjectAverage_NotCountedAsZero()
    {
        var result = GradeSheetAverages.Compute(
            [Bucket(1, 60m), Bucket(2, 40m)],
            [Grade(1, 1, 2.0m)]);

        Assert.Null(result.BucketAverages[2]);
        Assert.Equal(2.0m, result.SubjectAverage);
    }

    [Fact]
    public void Compute_SkipsVoidedGrades()
    {
        var result = GradeSheetAverages.Compute(
            [Bucket(1, 100m)],
            [Grade(1, 1, 2.0m), Grade(2, 1, 6.0m, voidedOn: new DateTime(2025, 10, 1))]);

        Assert.Equal(2.0m, result.BucketAverages[1]);
        Assert.Equal(2.0m, result.SubjectAverage);
    }

    [Fact]
    public void Compute_ReturnsNullAverages_WhenEveryGradeIsVoided()
    {
        var result = GradeSheetAverages.Compute(
            [Bucket(1, 100m)],
            [Grade(1, 1, 2.0m, voidedOn: new DateTime(2025, 10, 1))]);

        Assert.Null(result.BucketAverages[1]);
        Assert.Null(result.SubjectAverage);
    }

    [Fact]
    public void Compute_IgnoresGradesOfOtherBuckets()
    {
        var result = GradeSheetAverages.Compute([Bucket(1, 100m)], [Grade(1, 1, 2.0m), Grade(2, 99, 6.0m)]);

        Assert.Equal(2.0m, result.BucketAverages[1]);
    }

    [Fact]
    public void Compute_KeepsDecimalPrecision_WithoutRounding()
    {
        var result = GradeSheetAverages.Compute(
            [Bucket(1, 100m)],
            [Grade(1, 1, 1.0m), Grade(2, 1, 1.3m)]);

        Assert.Equal(1.15m, result.BucketAverages[1]);
    }

    [Fact]
    public void ProposedFinalGrade_UsesSnapshottedWeights_ForSubjectAverage()
    {
        var result = GradeSheetAverages.Compute(
            [Bucket(1, 50m), Bucket(2, 50m)],
            [Grade(1, 1, 2.0m), Grade(2, 2, 3.0m)]);

        Assert.Equal(2.5m, result.SubjectAverage);
        Assert.Equal(3m, result.ProposedFinalGrade);
    }

    [Fact]
    public void ProposedFinalGrade_IgnoresVoidedGrades()
    {
        var result = GradeSheetAverages.Compute(
            [Bucket(1, 100m)],
            [Grade(1, 1, 2.0m), Grade(2, 1, 6.0m, voidedOn: new DateTime(2025, 10, 1))]);

        Assert.Equal(2m, result.ProposedFinalGrade);
    }

    [Fact]
    public void ProposedFinalGrade_IsNull_ForNoGrades()
    {
        Assert.Null(GradeSheetAverages.Compute([Bucket(1, 100m)], []).ProposedFinalGrade);
    }

    [Fact]
    public void Compute_Throws_ForNullArguments()
    {
        Assert.Throws<ArgumentNullException>(() => GradeSheetAverages.Compute(null!, []));
        Assert.Throws<ArgumentNullException>(() => GradeSheetAverages.Compute([], null!));
    }
}
