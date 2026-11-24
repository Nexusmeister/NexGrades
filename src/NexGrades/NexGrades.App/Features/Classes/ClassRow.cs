using NexGrades.App.Localization;
using NexGrades.Data.Entities;

namespace NexGrades.App.Features.Classes;

/// <summary>One class of the active school year, as listed on the classes overview.</summary>
/// <param name="Id">The class instance id.</param>
/// <param name="Name">The class name in this school year, e.g. "5A".</param>
/// <param name="CohortLabel">The cohort name shared across school years, taken from the class group.</param>
/// <param name="StudentCount">The number of students who are enrolled and have not left.</param>
/// <param name="Status">The lifecycle status of the class instance.</param>
public sealed record ClassRow(int Id, string Name, string CohortLabel, int StudentCount, ClassInstanceStatus Status)
{
    /// <summary>Whether the row shows a status badge; an active class needs none.</summary>
    public bool HasStatusBadge => Status != ClassInstanceStatus.Active;

    /// <summary>The badge text for a rolled-forward or archived class; empty for an active one.</summary>
    public string StatusText => Status switch
    {
        ClassInstanceStatus.RolledForward => TextResources.Text("Classes_Status_RolledForward"),
        ClassInstanceStatus.Archived => TextResources.Text("Classes_Status_Archived"),
        _ => string.Empty,
    };
}
