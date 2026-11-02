using NexGrades.Data.Entities;

namespace NexGrades.Domain.Services;

/// <summary>Where one class of a school year stands in the end-of-year flow.</summary>
/// <param name="ClassInstanceId">The class instance id.</param>
/// <param name="Name">The class name in this school year.</param>
/// <param name="Status">Active means no decision yet; RolledForward and Archived mean it is done.</param>
/// <param name="CarryOverStudentCount">Students that carry over (Active), that carried over (RolledForward), or 0 (Archived).</param>
/// <param name="SuggestedName">A proposed name for the next year, or empty when none can be derived.</param>
/// <param name="SuccessorClassInstanceId">The class's successor in the target year, when one exists.</param>
/// <param name="SuccessorName">The successor's name.</param>
/// <param name="IsClassGroupEnded">Whether the cohort has been ended, for example by archiving a class with that choice.</param>
public sealed record ClassRolloverState(
    int ClassInstanceId,
    string Name,
    ClassInstanceStatus Status,
    int CarryOverStudentCount,
    string SuggestedName,
    int? SuccessorClassInstanceId,
    string? SuccessorName,
    bool IsClassGroupEnded);
