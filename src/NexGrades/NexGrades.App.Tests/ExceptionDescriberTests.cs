using NexGrades.App.Logging;
using NexGrades.Domain.Exceptions;

namespace NexGrades.App.Tests;

public class ExceptionDescriberTests
{
    private const string Secret = "ZZ-SECRET";

    private static InvariantViolationException ThrownDomainException()
    {
        try
        {
            throw new InvariantViolationException($"student {Secret} is wrong");
        }
        catch (InvariantViolationException ex)
        {
            return ex;
        }
    }

    [Fact]
    public void Describe_RedactsMessageButKeepsTypeAndStack_ForDomainException()
    {
        var text = ExceptionDescriber.Describe(ThrownDomainException());

        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        Assert.Contains(typeof(InvariantViolationException).FullName!, text, StringComparison.Ordinal);
        Assert.Contains(ExceptionDescriber.RedactedMessage, text, StringComparison.Ordinal);
        Assert.Contains(nameof(ThrownDomainException), text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_RedactsDomainMessage_InsideAggregateException()
    {
        var aggregate = new AggregateException("outer", ThrownDomainException(), new InvalidOperationException("other"));

        var text = ExceptionDescriber.Describe(aggregate);

        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        Assert.Contains("other", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_RedactsAggregateMessage_WhenItEmbedsADomainMessage()
    {
        // The default AggregateException message embeds the inner messages.
        var aggregate = new AggregateException(ThrownDomainException());
        Assert.Contains(Secret, aggregate.Message, StringComparison.Ordinal);

        var text = ExceptionDescriber.Describe(aggregate);

        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_RedactsDomainException_NestedAsInnerException()
    {
        var outer = new InvalidOperationException("outer message", ThrownDomainException());

        var text = ExceptionDescriber.Describe(outer);

        Assert.DoesNotContain(Secret, text, StringComparison.Ordinal);
        Assert.Contains("outer message", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_KeepsMessage_ForFrameworkException()
    {
        var text = ExceptionDescriber.Describe(new InvalidOperationException("framework says no"));

        Assert.Contains("System.InvalidOperationException: framework says no", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_RendersInnerExceptionChain()
    {
        var outer = new InvalidOperationException("outer", new InvalidCastException("middle", new FormatException("innermost")));

        var text = ExceptionDescriber.Describe(outer);

        Assert.Contains("outer", text, StringComparison.Ordinal);
        Assert.Contains("System.InvalidCastException: middle", text, StringComparison.Ordinal);
        Assert.Contains("System.FormatException: innermost", text, StringComparison.Ordinal);
        Assert.Equal(2, text.Split("--- Inner exception ---").Length - 1);
    }

    [Fact]
    public void Describe_RendersEveryInnerException_OfAnAggregate()
    {
        var aggregate = new AggregateException("many", new FormatException("first"), new FormatException("second"));

        var text = ExceptionDescriber.Describe(aggregate);

        Assert.Contains("first", text, StringComparison.Ordinal);
        Assert.Contains("second", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Describe_Throws_ForNull() =>
        Assert.Throws<ArgumentNullException>(() => ExceptionDescriber.Describe(null!));
}
