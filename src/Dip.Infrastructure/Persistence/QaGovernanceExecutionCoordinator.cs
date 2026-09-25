// Purpose: Implements QA Governance Execution Coordinator infrastructure behavior for Persistence.
using System.Collections.Concurrent;
using System.Data;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence;

/// <summary>Exactly-once governance execution. PostgreSQL uses transaction-scoped advisory locks; non-relational test providers use an in-process keyed semaphore.</summary>
public sealed class QaGovernanceExecutionCoordinator(DipDbContext db) : IQaGovernanceExecutionCoordinator
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> LocalLocks = new();
    public async Task<bool> ExecuteOnceAsync(Guid projectId, Guid qaRunId, string governanceVersion, string inputHash, Func<CancellationToken, Task<string>> action, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return await ExecuteLocalAsync(projectId, qaRunId, governanceVersion, inputHash, action, ct);
        var strategy = db.Database.CreateExecutionStrategy(); var executed = false;
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if ((db.Database.ProviderName ?? string.Empty).Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({qaRunId.ToString("D")}));", ct);
            var existing = await db.QaGovernanceProcessedRuns.SingleOrDefaultAsync(x => x.QaRunId == qaRunId, ct);
            if (existing is not null) ValidateExisting(existing, projectId, governanceVersion, inputHash);
            if (existing?.Status == Dip.Domain.Enums.QaProcessedRunStatus.Completed) { await tx.CommitAsync(ct); return; }
            if (existing is null)
            {
                existing = new QaGovernanceProcessedRun(Guid.NewGuid(), projectId, qaRunId, governanceVersion, inputHash, DateTimeOffset.UtcNow);
                await db.QaGovernanceProcessedRuns.AddAsync(existing, ct);
                await db.SaveChangesAsync(ct);
            }
            var resultHash = await action(ct); existing.Complete(resultHash, DateTimeOffset.UtcNow); await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); executed = true;
        });
        return executed;
    }
    private static void ValidateExisting(QaGovernanceProcessedRun existing, Guid projectId, string governanceVersion, string inputHash)
    {
        if (existing.ProjectId != projectId) throw new InvalidOperationException("QaRunId is already bound to a different project.");
        if (!string.Equals(existing.GovernanceVersion, governanceVersion, StringComparison.Ordinal)) throw new InvalidOperationException("QaRunId is already bound to a different governance version.");
        if (!string.Equals(existing.InputHash, inputHash, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("The same QaRunId was submitted with different governance input evidence.");
    }

    private async Task<bool> ExecuteLocalAsync(Guid projectId, Guid qaRunId, string governanceVersion, string inputHash, Func<CancellationToken, Task<string>> action, CancellationToken ct)
    {
        var gate = LocalLocks.GetOrAdd(qaRunId, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(ct);
        try
        {
            var existing = await db.QaGovernanceProcessedRuns.SingleOrDefaultAsync(x => x.QaRunId == qaRunId, ct);
            if (existing is not null) ValidateExisting(existing, projectId, governanceVersion, inputHash);
            if (existing?.Status == Dip.Domain.Enums.QaProcessedRunStatus.Completed) return false;
            if (existing is null)
            {
                existing = new QaGovernanceProcessedRun(Guid.NewGuid(), projectId, qaRunId, governanceVersion, inputHash, DateTimeOffset.UtcNow);
                await db.QaGovernanceProcessedRuns.AddAsync(existing, ct);
                await db.SaveChangesAsync(ct);
            }
            var hash = await action(ct);
            existing.Complete(hash, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(ct);
            return true;
        }
        finally
        {
            gate.Release();
        }
    }
}
