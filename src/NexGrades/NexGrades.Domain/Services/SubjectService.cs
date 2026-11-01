using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Services;

/// <summary>
/// Adds subjects to the catalogue. A subject is not year-scoped.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
public sealed class SubjectService(IDbContextFactory<AppDbContext> contextFactory)
{
    /// <summary>Maximum length of a subject's name, after trimming.</summary>
    public const int MaxNameLength = 50;

    /// <summary>Maximum length of a subject's short code, after trimming.</summary>
    public const int MaxShortCodeLength = 10;

    /// <summary>Adds a subject to the catalogue.</summary>
    /// <exception cref="InvariantViolationException">The name or short code is empty or too long.</exception>
    public Task<Subject> CreateSubjectAsync(
        string name, string shortCode, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SubjectService.CreateSubject", async () =>
        {
            var normalizedName = RequiredText.Normalize(name, "A subject's name", MaxNameLength);
            var normalizedShortCode = RequiredText.Normalize(shortCode, "A subject's short code", MaxShortCodeLength);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var subject = new Subject
            {
                Name = normalizedName,
                ShortCode = normalizedShortCode,
            };
            context.Subjects.Add(subject);
            await context.SaveChangesAsync(cancellationToken);

            return subject;
        });
}
