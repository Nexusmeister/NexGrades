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
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();

    public DbSet<Teacher> Teachers => Set<Teacher>();

    public DbSet<ClassGroup> ClassGroups => Set<ClassGroup>();

    public DbSet<ClassInstance> ClassInstances => Set<ClassInstance>();

    public DbSet<Student> Students => Set<Student>();

    public DbSet<Enrollment> Enrollments => Set<Enrollment>();

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<BucketTemplate> BucketTemplates => Set<BucketTemplate>();

    public DbSet<SubjectInstance> SubjectInstances => Set<SubjectInstance>();

    public DbSet<GradeBucket> GradeBuckets => Set<GradeBucket>();

    public DbSet<SubjectParticipation> SubjectParticipations => Set<SubjectParticipation>();

    public DbSet<Grade> Grades => Set<Grade>();

    public DbSet<Note> Notes => Set<Note>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

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
