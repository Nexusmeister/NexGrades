namespace NexGrades.App.Infrastructure;

/// <summary>
/// Where the app database lives. <paramref name="IsOverride"/> is true when a connection string was
/// configured explicitly (Aspire in Development, or the <c>sqlite</c> key); then the app must not create
/// folders or relocate legacy files, because the location is not ours to manage.
/// </summary>
/// <param name="ConnectionString">The SQLite connection string that the database context uses.</param>
/// <param name="IsOverride">True when the connection string was configured explicitly, so the location is not managed by the app.</param>
public sealed record DatabaseLocation(string ConnectionString, bool IsOverride);
