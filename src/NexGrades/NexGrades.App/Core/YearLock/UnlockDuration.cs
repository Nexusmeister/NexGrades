namespace NexGrades.App.Core.YearLock;

/// <summary>Turns an <see cref="UnlockDurationOption"/> into the moment the unlock ends.</summary>
public static class UnlockDuration
{
    /// <summary>How many days ahead a custom end date may be; a UI limit only, the Domain sets none.</summary>
    public const int MaxCustomDays = 30;

    /// <summary>
    /// Resolves the end of the unlock. A custom date ends at the last moment of that day, like "until the end of today".
    /// </summary>
    /// <param name="option">The chosen duration.</param>
    /// <param name="customDate">The picked date; only used for <see cref="UnlockDurationOption.CustomDate"/>.</param>
    /// <param name="now">The current time.</param>
    /// <returns>The end of the unlock, or <see langword="null"/> for a custom option without a date.</returns>
    public static DateTime? Resolve(UnlockDurationOption option, DateTime? customDate, DateTime now) => option switch
    {
        UnlockDurationOption.OneHour => now.AddHours(1),
        UnlockDurationOption.EndOfToday => EndOfDay(now),
        UnlockDurationOption.OneWeek => now.AddDays(7),
        UnlockDurationOption.CustomDate => customDate is { } date ? EndOfDay(date) : null,
        _ => throw new ArgumentOutOfRangeException(nameof(option), option, "Unknown unlock duration."),
    };

    private static DateTime EndOfDay(DateTime day) => day.Date.AddDays(1).AddTicks(-1);
}
