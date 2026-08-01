using System.Globalization;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using NexGrades.Data.Entities;
using NexGrades.Domain.Exceptions;
using NexGrades.Domain.Time;

namespace NexGrades.Domain.Auditing;

/// <summary>
/// Enforces "no write to any entity reachable from a <c>Closed</c> <see cref="SchoolYear"/> unless that year
/// is explicitly unlocked, and every such write produces an <see cref="AuditEntry"/>".
/// </summary>
/// <remarks>
/// <para>
/// <b>Two-phase design.</b> <c>SavingChanges</c>/<c>SavingChangesAsync</c> run before the underlying
/// <c>INSERT</c>/<c>UPDATE</c> statements execute: this is where a locked-closed-year write is rejected, by
/// throwing before <c>base.SavingChanges(Async)</c> is invoked, so nothing is persisted. For allowed writes
/// to an unlocked closed year, the property-level diffs that will become <see cref="AuditEntry"/> rows are
/// only *collected* at this point, because a newly <c>Added</c> entity's <c>Id</c> is still a temporary,
/// not-yet-persisted value here. <c>SavedChanges</c>/<c>SavedChangesAsync</c> then run after the real save
/// has completed and EF Core has fixed up the real, database-assigned <c>Id</c> values onto the same tracked
/// entity instances; this is where the actual <see cref="AuditEntry"/> rows are built and written, via a
/// second, small <c>SaveChanges</c> call on the same context. This second call re-enters this interceptor,
/// but the audit rows it adds resolve to no school year (<see cref="AuditEntry"/> is explicitly out of scope
/// for this mechanism — see <see cref="SchoolYearResolver"/>), so nothing further is collected and the
/// recursion terminates after one extra pass.
/// </para>
/// <para>
/// <b>Atomicity.</b> The primary write and the <see cref="AuditEntry"/> rows it produces must commit or roll
/// back together — otherwise a failure while writing the audit rows (the second, nested <c>SaveChanges</c>
/// call above) could leave the primary edit durably persisted with no audit trail at all. EF Core would
/// normally commit the primary write's own implicit transaction before <c>SavedChanges</c> even runs, so this
/// interceptor owns an ambient transaction spanning both phases whenever one isn't already open: if
/// <c>SavingChanges</c>/<c>SavingChangesAsync</c> finds a write that will require auditing and
/// <see cref="DbContext.Database"/>'s <see cref="DatabaseFacade.CurrentTransaction"/> is <see langword="null"/>,
/// it begins one and remembers that it owns it. <c>SavedChanges</c>/<c>SavedChangesAsync</c> then commits and
/// disposes that transaction only after the nested audit-row save succeeds, and rolls it back on any failure
/// in either phase. If the caller already had its own transaction open (as <see cref="Services.EnrollmentService"/>
/// and <see cref="Services.RolloverService"/> do), this interceptor leaves it alone entirely and simply
/// participates in it — the caller's own commit then durably includes the audit rows too, since they share
/// the same transaction.
/// </para>
/// <para>
/// <b>Deletes.</b> The model does not support deleting rows at all: every entity transitions status instead
/// of being removed. This interceptor therefore refuses <em>any</em> <c>Deleted</c> entry outright,
/// unconditionally, for every entity type — not only ones reachable from a closed school year — rather than
/// silently allowing history to disappear.
/// </para>
/// </remarks>
public sealed class ClosedYearAuditInterceptor(IClock clock, IUnlockReasonStore reasonStore) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, ContextState> _stateByContext = new();

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        var context = RequireContext(eventData);
        var state = _stateByContext.GetValue(context, static _ => new ContextState());

        state.Pending = CollectPending(context);

        if (!state.IsMaterializing && state.Pending.Count > 0 && context.Database.CurrentTransaction is null)
        {
            // No caller-provided transaction is open: own one ourselves so the primary write below and the
            // AuditEntry rows added in SavedChanges commit — or roll back — together.
            state.OwnedTransaction = context.Database.BeginTransaction();
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var context = RequireContext(eventData);
        var state = _stateByContext.GetValue(context, static _ => new ContextState());

        state.Pending = await CollectPendingAsync(context, cancellationToken);

        if (!state.IsMaterializing && state.Pending.Count > 0 && context.Database.CurrentTransaction is null)
        {
            // No caller-provided transaction is open: own one ourselves so the primary write below and the
            // AuditEntry rows added in SavedChangesAsync commit — or roll back — together.
            state.OwnedTransaction = await context.Database.BeginTransactionAsync(cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        if (eventData.Context is { } context
            && _stateByContext.TryGetValue(context, out var state)
            && !state.IsMaterializing
            && state.Pending.Count > 0)
        {
            var pending = state.Pending;
            state.Pending = [];
            state.IsMaterializing = true;
            try
            {
                MaterializeAuditEntries(context, pending);
                state.OwnedTransaction?.Commit();
            }
            catch
            {
                // If the nested save above already failed, SaveChangesFailed already rolled this back and
                // cleared it; this is then a safe no-op. It also covers the (rarer) case of a failure before
                // the nested SaveChanges call is even reached.
                state.OwnedTransaction?.Rollback();
                throw;
            }
            finally
            {
                state.OwnedTransaction?.Dispose();
                state.OwnedTransaction = null;
                state.IsMaterializing = false;
            }
        }

        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context
            && _stateByContext.TryGetValue(context, out var state)
            && !state.IsMaterializing
            && state.Pending.Count > 0)
        {
            var pending = state.Pending;
            state.Pending = [];
            state.IsMaterializing = true;
            try
            {
                await MaterializeAuditEntriesAsync(context, pending, cancellationToken);
                if (state.OwnedTransaction is { } transactionToCommit)
                {
                    await transactionToCommit.CommitAsync(cancellationToken);
                }
            }
            catch
            {
                // If the nested save above already failed, SaveChangesFailedAsync already rolled this back
                // and cleared it; this is then a safe no-op. It also covers the (rarer) case of a failure
                // before the nested SaveChangesAsync call is even reached.
                if (state.OwnedTransaction is { } transactionToRollBack)
                {
                    await transactionToRollBack.RollbackAsync(cancellationToken);
                }

                throw;
            }
            finally
            {
                if (state.OwnedTransaction is { } transactionToDispose)
                {
                    await transactionToDispose.DisposeAsync();
                }

                state.OwnedTransaction = null;
                state.IsMaterializing = false;
            }
        }

        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context is { } context && _stateByContext.TryGetValue(context, out var state))
        {
            state.OwnedTransaction?.Rollback();
            state.OwnedTransaction?.Dispose();
            state.OwnedTransaction = null;
            state.Pending = [];
            state.IsMaterializing = false;
        }

        base.SaveChangesFailed(eventData);
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context && _stateByContext.TryGetValue(context, out var state))
        {
            if (state.OwnedTransaction is { } transaction)
            {
                await transaction.RollbackAsync(cancellationToken);
                await transaction.DisposeAsync();
            }

            state.OwnedTransaction = null;
            state.Pending = [];
            state.IsMaterializing = false;
        }

        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private static DbContext RequireContext(DbContextEventData eventData) =>
        eventData.Context ?? throw new InvalidOperationException("SaveChanges event raised without a DbContext.");

    private List<PendingAudit> CollectPending(DbContext context)
    {
        context.ChangeTracker.DetectChanges();
        var pending = new List<PendingAudit>();

        foreach (var entry in RelevantEntries(context))
        {
            var schoolYearId = SchoolYearResolver.Resolve(context, entry.Entity);
            if (TryBuildPendingAudit(context, entry, schoolYearId, out var auditEntry))
            {
                pending.Add(auditEntry);
            }
        }

        return pending;
    }

    private async Task<List<PendingAudit>> CollectPendingAsync(DbContext context, CancellationToken cancellationToken)
    {
        context.ChangeTracker.DetectChanges();
        var pending = new List<PendingAudit>();

        foreach (var entry in RelevantEntries(context))
        {
            var schoolYearId = await SchoolYearResolver.ResolveAsync(context, entry.Entity, cancellationToken);
            if (TryBuildPendingAudit(context, entry, schoolYearId, out var auditEntry))
            {
                pending.Add(auditEntry);
            }
        }

        return pending;
    }

    private static IReadOnlyList<EntityEntry> RelevantEntries(DbContext context) =>
        context.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .ToList();

    /// <summary>
    /// Checks the closed/unlocked state for one changed entry and either returns a pending audit record to
    /// collect (unlocked closed year), returns <see langword="false"/> (out of scope, or an open year — no
    /// restriction, nothing to audit), or throws (a delete, always and unconditionally; or a write to a
    /// locked closed year).
    /// </summary>
    private bool TryBuildPendingAudit(DbContext context, EntityEntry entry, int? schoolYearId, out PendingAudit auditEntry)
    {
        auditEntry = null!;

        var schoolYear = schoolYearId is { } yearId ? context.Find<SchoolYear>(yearId) : null;

        if (entry.State == EntityState.Deleted)
        {
            // Nothing is ever deleted in this model — status transitions only. Reject every delete
            // unconditionally, regardless of whether the entity is reachable from any school year at all,
            // let alone a closed one.
            if (schoolYear is { Status: SchoolYearStatus.Closed })
            {
                throw new ClosedYearException(
                    $"Deleting a {entry.Metadata.ClrType.Name} that belongs to closed school year {schoolYear.Id} ('{schoolYear.Label}') " +
                    "is not supported, even while the year is unlocked.");
            }

            throw new InvariantViolationException(
                $"Deleting a {entry.Metadata.ClrType.Name} is not supported; nothing is ever deleted in this model, only status-transitioned.");
        }

        if (schoolYear is null || schoolYear.Status != SchoolYearStatus.Closed)
        {
            return false; // Not closed (or no year association at all): no restriction, no audit trail.
        }

        var isUnlocked = schoolYear.UnlockedUntil is { } until && until > clock.Now;
        if (!isUnlocked)
        {
            throw new ClosedYearException(
                $"School year {schoolYear.Id} ('{schoolYear.Label}') is closed and not currently unlocked; " +
                $"cannot write to {entry.Metadata.ClrType.Name} (state: {entry.State}).");
        }

        var reason = reasonStore.GetReason(schoolYear.Id);
        var changes = entry.State == EntityState.Added
            ? [new PendingPropertyChange("<created>", null, FormatCreatedSummary(entry))]
            : ModifiedPropertyChanges(entry);

        if (changes.Count == 0)
        {
            return false; // Modified entry with no actual scalar changes (e.g. only a navigation touched).
        }

        auditEntry = new PendingAudit(entry.Entity, entry.Metadata.ClrType.Name, reason, changes);
        return true;
    }

    private static List<PendingPropertyChange> ModifiedPropertyChanges(EntityEntry entry) =>
        entry.Properties
            .Where(p => p.IsModified && !p.Metadata.IsPrimaryKey())
            .Select(p => new PendingPropertyChange(p.Metadata.Name, FormatValue(p.OriginalValue), FormatValue(p.CurrentValue)))
            .ToList();

    private static string FormatCreatedSummary(EntityEntry entry) =>
        string.Join(
            "; ",
            entry.Properties
                .Where(p => !p.Metadata.IsPrimaryKey())
                .Select(p => $"{p.Metadata.Name}={FormatValue(p.CurrentValue) ?? "<null>"}"));

    private static string? FormatValue(object? value) => value switch
    {
        null => null,
        DateTime dateTime => dateTime.ToString("O", CultureInfo.InvariantCulture),
        DateOnly dateOnly => dateOnly.ToString("O", CultureInfo.InvariantCulture),
        decimal number => number.ToString(CultureInfo.InvariantCulture),
        Enum enumValue => enumValue.ToString(),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture),
    };

    private void MaterializeAuditEntries(DbContext context, List<PendingAudit> pending)
    {
        AddAuditEntries(context, pending);
        context.SaveChanges();
    }

    private async Task MaterializeAuditEntriesAsync(DbContext context, List<PendingAudit> pending, CancellationToken cancellationToken)
    {
        AddAuditEntries(context, pending);
        await context.SaveChangesAsync(cancellationToken);
    }

    private void AddAuditEntries(DbContext context, List<PendingAudit> pending)
    {
        var auditEntries = context.Set<AuditEntry>();
        var now = clock.Now;

        foreach (var item in pending)
        {
            var entityId = GetEntityId(context.Entry(item.Entity));

            foreach (var change in item.Changes)
            {
                auditEntries.Add(new AuditEntry
                {
                    EntityType = item.EntityType,
                    EntityId = entityId,
                    Field = change.Field,
                    OldValue = change.OldValue,
                    NewValue = change.NewValue,
                    ChangedOn = now,
                    Reason = item.Reason,
                });
            }
        }
    }

    /// <summary>
    /// Resolves an entry's primary key value generically via EF Core's model metadata, rather than assuming a
    /// property literally named "Id" of type <see cref="int"/> — every entity in this model happens to match
    /// that shape today, but this is the standard, shape-independent way to ask EF Core for it.
    /// </summary>
    private static int GetEntityId(EntityEntry entry)
    {
        var primaryKey = entry.Metadata.FindPrimaryKey()
            ?? throw new InvalidOperationException($"{entry.Metadata.ClrType.Name} has no primary key configured.");
        var keyProperty = primaryKey.Properties[0];
        return (int)entry.Property(keyProperty.Name).CurrentValue!;
    }

    /// <summary>
    /// Per-<see cref="DbContext"/>-instance state for the two-phase, transaction-owning design above. Kept in
    /// a <see cref="ConditionalWeakTable{TKey,TValue}"/> since one interceptor instance is shared across every
    /// <see cref="DbContext"/> the host application creates.
    /// </summary>
    private sealed class ContextState
    {
        /// <summary>The audit diffs collected by the most recent <c>SavingChanges</c>/<c>SavingChangesAsync</c> call.</summary>
        public List<PendingAudit> Pending { get; set; } = [];

        /// <summary>
        /// The transaction this interceptor began itself (because no caller-provided one was open) for the
        /// write currently being audited, or <see langword="null"/> if none is owned right now — either
        /// because there is nothing to audit, or because a caller already owns one.
        /// </summary>
        public IDbContextTransaction? OwnedTransaction { get; set; }

        /// <summary>
        /// True for the duration of the nested <c>SaveChanges</c> call that writes the <see cref="AuditEntry"/>
        /// rows, so the recursive re-entry into <c>SavingChanges</c>/<c>SavedChanges</c> doesn't try to open a
        /// second transaction or materialize audit entries for the audit entries themselves.
        /// </summary>
        public bool IsMaterializing { get; set; }
    }

    private sealed record PendingPropertyChange(string Field, string? OldValue, string? NewValue);

    private sealed record PendingAudit(object Entity, string EntityType, string? Reason, IReadOnlyList<PendingPropertyChange> Changes);
}
