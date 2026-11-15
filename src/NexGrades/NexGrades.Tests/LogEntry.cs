using Microsoft.Extensions.Logging;

namespace NexGrades.Tests;

/// <summary>One captured log call: level, rendered message and attached exception.</summary>
internal sealed record LogEntry(LogLevel Level, string Message, Exception? Exception);
