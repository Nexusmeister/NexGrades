using NexGrades.Domain.Grading;

namespace NexGrades.Tests;

public class AverageCalculatorTests
{
    [Fact]
    public void BucketAverage_ReturnsNull_ForNoGrades()
    {
        Assert.Null(AverageCalculator.BucketAverage([]));
    }

    [Fact]
    public void BucketAverage_ReturnsPlainArithmeticMean()
    {
        var average = AverageCalculator.BucketAverage([1.0m, 2.0m, 3.0m]);

        Assert.Equal(2.0m, average);
    }

    [Fact]
    public void SubjectAverage_ReturnsNull_WhenNoBucketHasAnyGrades()
    {
        var buckets = new[]
        {
            new AverageCalculator.WeightedBucketAverage(60m, null),
            new AverageCalculator.WeightedBucketAverage(40m, null),
        };

        Assert.Null(AverageCalculator.SubjectAverage(buckets));
    }

    [Fact]
    public void SubjectAverage_WeightsByBucketWeight_NotByGradeCount()
    {
        // Written (weight 70) average 2.0, Oral (weight 30) average 4.0.
        // Weighted: (2.0*70 + 4.0*30) / (70+30) = (140+120)/100 = 2.6.
        var buckets = new[]
        {
            new AverageCalculator.WeightedBucketAverage(70m, 2.0m),
            new AverageCalculator.WeightedBucketAverage(30m, 4.0m),
        };

        var average = AverageCalculator.SubjectAverage(buckets);

        Assert.Equal(2.6m, average);
    }

    [Fact]
    public void SubjectAverage_NormalisesByActualWeightSum_WhenABucketIsStillEmpty()
    {
        // Oral bucket (weight 30) has no grades yet: normalise over the 70 that does, not over 100.
        var buckets = new[]
        {
            new AverageCalculator.WeightedBucketAverage(70m, 2.0m),
            new AverageCalculator.WeightedBucketAverage(30m, null),
        };

        var average = AverageCalculator.SubjectAverage(buckets);

        Assert.Equal(2.0m, average);
    }

    [Fact]
    public void SubjectAverage_StaysExact_NotFloatingPointDrift()
    {
        // A classic case that produces repeating decimals in double/float but must stay an exact decimal here.
        var buckets = new[]
        {
            new AverageCalculator.WeightedBucketAverage(1m, 2.4m),
            new AverageCalculator.WeightedBucketAverage(1m, 2.4m),
            new AverageCalculator.WeightedBucketAverage(1m, 2.4m),
        };

        var average = AverageCalculator.SubjectAverage(buckets);

        Assert.Equal(2.4m, average);
    }
}
