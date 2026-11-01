using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Services;

/// <summary>
/// Shared trimming and length rules for required free-text fields. The offending value is never put into the
/// exception message, because it can be pupil data.
/// </summary>
internal static class RequiredText
{
    /// <summary>Trims <paramref name="value"/> and checks that it is not empty and not longer than <paramref name="maxLength"/>.</summary>
    /// <param name="value">The raw input.</param>
    /// <param name="description">Names the field in the exception message, e.g. "A class name".</param>
    /// <param name="maxLength">Maximum length after trimming, or <see langword="null"/> for no limit.</param>
    /// <exception cref="InvariantViolationException">The trimmed value is empty or too long.</exception>
    public static string Normalize(string? value, string description, int? maxLength = null)
    {
        var trimmed = value?.Trim() ?? string.Empty;
        if (trimmed.Length == 0)
        {
            throw new InvariantViolationException($"{description} must not be empty.");
        }

        if (maxLength is { } max && trimmed.Length > max)
        {
            throw new InvariantViolationException(
                $"{description} must not exceed {max} characters (was {trimmed.Length}).");
        }

        return trimmed;
    }
}
