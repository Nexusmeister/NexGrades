namespace NexGrades.App.Core.YearLock;

/// <summary>How long a closed school year stays unlocked, as offered in the unlock dialog.</summary>
public enum UnlockDurationOption
{
    /// <summary>One hour from now. The default: a correction is usually quick.</summary>
    OneHour,

    /// <summary>Until 23:59 today.</summary>
    EndOfToday,

    /// <summary>Seven days from now.</summary>
    OneWeek,

    /// <summary>Until 23:59 of a date the teacher picks.</summary>
    CustomDate,
}
