using Microsoft.EntityFrameworkCore;
using NexGrades.Data;
using NexGrades.Data.Entities;
using NexGrades.Domain.Auditing;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Services;

/// <summary>
/// Creates students, enrolling them into a class straight away, and renames them.
/// </summary>
/// <param name="contextFactory">Creates the short-lived context that each call opens.</param>
/// <param name="clock">Source of the enrollment's join time.</param>
/// <remarks>
/// A student exists only to be taught, so creating one and enrolling them is a single atomic operation.
/// Writes to closed years are rejected and audited by <see cref="ClosedYearAuditInterceptor"/>.
/// </remarks>
public sealed class StudentService(IDbContextFactory<AppDbContext> contextFactory, IClock clock)
{
    /// <summary>Maximum length of a first or last name, after trimming.</summary>
    public const int MaxNameLength = 50;

    /// <summary>
    /// Creates a student and enrolls them into <paramref name="classInstanceId"/>, in one transaction.
    /// </summary>
    /// <exception cref="InvariantViolationException">
    /// A name is empty or too long, the class does not exist or is not Active, its school year is not the
    /// Active one, or the new student already has an enrollment in that year.
    /// </exception>
    public Task<Student> CreateStudentAsync(
        string firstName,
        string lastName,
        int classInstanceId,
        CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("StudentService.CreateStudent", async () =>
        {
            var normalizedFirstName = RequiredText.Normalize(firstName, "A student's first name", MaxNameLength);
            var normalizedLastName = RequiredText.Normalize(lastName, "A student's last name", MaxNameLength);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);

            var classInstance = await context.ClassInstances
                .Include(ci => ci.SchoolYear)
                .FirstOrDefaultAsync(ci => ci.Id == classInstanceId, cancellationToken)
                ?? throw new InvariantViolationException($"Class instance {classInstanceId} does not exist.");

            if (classInstance.SchoolYear.Status != SchoolYearStatus.Active)
            {
                throw new InvariantViolationException(
                    $"Class instance {classInstanceId} belongs to school year {classInstance.SchoolYearId} " +
                    $"(status {classInstance.SchoolYear.Status}); a new student can only be enrolled into a class of the Active school year.");
            }

            if (classInstance.Status != ClassInstanceStatus.Active)
            {
                throw new InvariantViolationException(
                    $"Class instance {classInstanceId} is {classInstance.Status}; " +
                    "a new student can only be enrolled into an Active class.");
            }

            var student = new Student
            {
                FirstName = normalizedFirstName,
                LastName = normalizedLastName,
            };
            context.Students.Add(student);
            await context.SaveChangesAsync(cancellationToken);

            await EnrollmentInvariants.EnsureNoConflictingEnrollmentAsync(
                context, student.Id, classInstance.SchoolYearId, cancellationToken);

            context.Enrollments.Add(new Enrollment
            {
                StudentId = student.Id,
                ClassInstanceId = classInstanceId,
                JoinedOn = clock.Now,
            });
            await context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return student;
        });

    /// <summary>
    /// Changes a student's first and last name. Saving identical names is a no-op.
    /// </summary>
    /// <exception cref="InvariantViolationException">A name is empty or too long, or the student does not exist.</exception>
    public Task RenameStudentAsync(
        int studentId, string firstName, string lastName, CancellationToken cancellationToken = default) =>
        DomainOperation.RunAsync("StudentService.RenameStudent", async () =>
        {
            var normalizedFirstName = RequiredText.Normalize(firstName, "A student's first name", MaxNameLength);
            var normalizedLastName = RequiredText.Normalize(lastName, "A student's last name", MaxNameLength);

            await using var context = await contextFactory.CreateDbContextAsync(cancellationToken);
            var student = await context.Students
                .FirstOrDefaultAsync(s => s.Id == studentId, cancellationToken)
                ?? throw new InvariantViolationException($"Student {studentId} does not exist.");

            if (student.FirstName == normalizedFirstName && student.LastName == normalizedLastName)
            {
                return;
            }

            student.FirstName = normalizedFirstName;
            student.LastName = normalizedLastName;
            await context.SaveChangesAsync(cancellationToken);
        });
}
