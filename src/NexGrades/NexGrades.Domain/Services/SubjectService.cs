using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;

namespace NexGrades.Domain.Services;

/// <summary>
/// Adds subjects to the catalogue and edits their name and short code. A subject is not year-scoped.
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
            var (normalizedName, normalizedShortCode) = Normalize(name, shortCode);

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

    /// <summary>
    /// Changes the name and short code of an existing subject. Bucket templates and subject instances are untouched.
    /// A subject is not year-scoped, so no closed-year lock applies; instances in past years show the new name.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// The subject does not exist, or the name or short code is empty or too long.
    /// </exception>
    public Task UpdateSubjectAsync(
        int subjectId, string name, string shortCode, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SubjectService.UpdateSubject", async () =>
        {
            var (normalizedName, normalizedShortCode) = Normalize(name, shortCode);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var subject = await context.Subjects
                .FirstOrDefaultAsync(s => s.Id == subjectId, cancellationToken)
                ?? throw new InvariantViolationException($"Subject {subjectId} does not exist.");

            subject.Name = normalizedName;
            subject.ShortCode = normalizedShortCode;
            await context.SaveChangesAsync(cancellationToken);
        });

    /// <summary>Reads one subject for the edit form; <see langword="null"/> when it does not exist.</summary>
    public Task<Subject?> FindSubjectAsync(int subjectId, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("SubjectService.FindSubject", async () =>
        {
            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            return await context.Subjects
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == subjectId, cancellationToken);
        });

    private static (string Name, string ShortCode) Normalize(string name, string shortCode) =>
        (RequiredText.Normalize(name, "A subject's name", MaxNameLength),
         RequiredText.Normalize(shortCode, "A subject's short code", MaxShortCodeLength));
}
