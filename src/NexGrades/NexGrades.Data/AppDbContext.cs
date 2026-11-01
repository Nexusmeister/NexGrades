using Microsoft.EntityFrameworkCore;
using NexGrades.Data.Entities;
using NexGrades.Data.EntityMapConfigs;

namespace NexGrades.Data;

/// <summary>
/// EF Core database context for NexGrades, backed by a single SQLite file. Modelling choices baked in via
/// the per-entity configurations in <see cref="EntityMapConfigs"/>: enums stored as strings so the raw .db
/// file stays human-readable; decimal grade/weight columns mapped to SQLite's TEXT storage class rather than
/// REAL, to avoid float drift in weighted averages; no cascade deletes anywhere — the model has no supported
/// delete operations at all, only status transitions, so every relationship uses <see cref="DeleteBehavior.Restrict"/>
/// as a safety net against an accidental delete cascading through history.
/// </summary>
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    /// <summary>The school years. Each one is the root scope that year-scoped rows are reached through.</summary>
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();

    /// <summary>The teachers, normally just the app owner.</summary>
    public DbSet<Teacher> Teachers => Set<Teacher>();

    /// <summary>The persistent cohorts that span school years.</summary>
    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();

    /// <summary>The yearly class instances, where teaching happens.</summary>
    public DbSet<ClassInstance> ClassInstances => Set<ClassInstance>();

    /// <summary>The students. Not year-scoped.</summary>
    public DbSet<Student> Students => Set<Student>();

    /// <summary>Student memberships of class instances. Enforced per school year by the Domain, not the schema.</summary>
    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    /// <summary>The subject catalogue. Not year-scoped.</summary>
    public DbSet<Subject> Subjects => Set<Subject>();

    /// <summary>The default bucket layouts owned by each subject.</summary>
    public DbSet<BucketTemplate> BucketTemplates => Set<BucketTemplate>();

    /// <summary>A subject taught to one class instance in one school year.</summary>
    public DbSet<SubjectInstance> SubjectInstances => Set<SubjectInstance>();

    /// <summary>The snapshotted grade buckets of each subject instance.</summary>
    public DbSet<GradeBucket> GradeBuckets => Set<GradeBucket>();

    /// <summary>Per-student overrides of subject participation. Holds rows only for deviations from the default.</summary>
    public DbSet<SubjectParticipation> SubjectParticipations => Set<SubjectParticipation>();

    /// <summary>The individual grades.</summary>
    public DbSet<Grade> Grades => Set<Grade>();

    /// <summary>The free-text notes about students.</summary>
    public DbSet<Note> Notes => Set<Note>();

    /// <summary>The change log written for edits to a closed, unlocked school year.</summary>
    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    /// <summary>
    /// Applies every entity configuration in <c>EntityMapConfigs</c>. A new entity is not mapped until its
    /// configuration is added here.
    /// </summary>
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new SchoolYearConfiguration());
        modelBuilder.ApplyConfiguration(new TeacherConfiguration());
        modelBuilder.ApplyConfiguration(new ClassGroupConfiguration());
        modelBuilder.ApplyConfiguration(new ClassInstanceConfiguration());
        modelBuilder.ApplyConfiguration(new StudentConfiguration());
        modelBuilder.ApplyConfiguration(new EnrollmentConfiguration());
        modelBuilder.ApplyConfiguration(new SubjectConfiguration());
        modelBuilder.ApplyConfiguration(new BucketTemplateConfiguration());
        modelBuilder.ApplyConfiguration(new SubjectInstanceConfiguration());
        modelBuilder.ApplyConfiguration(new GradeBucketConfiguration());
        modelBuilder.ApplyConfiguration(new SubjectParticipationConfiguration());
        modelBuilder.ApplyConfiguration(new GradeConfiguration());
        modelBuilder.ApplyConfiguration(new NoteConfiguration());
        modelBuilder.ApplyConfiguration(new AuditEntryConfiguration());
    }
}
