namespace NexGrades.App.Infrastructure;

/// <summary>
/// Where the app database lives. <paramref name="IsOverride"/> is true when a connection string was
/// configured explicitly (Aspire in Development, or the <c>sqlite</c> key); then the app must not create
/// folders or relocate legacy files, because the location is not ours to manage.
/// </summary>
public sealed record DatabaseLocation(string ConnectionString, bool IsOverride);
