// Purpose: Provides infrastructure behavior for this application boundary.
using System.Collections.Concurrent;
using System.Data;
using Dip.Application.Features.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence;

/// <summary>Cross-row project mutation lock. PostgreSQL uses a transaction-scoped advisory lock + SERIALIZABLE transaction.</summary>
public sealed class ProjectMutationCoordinator(DipDbContext db) : IProjectMutationCoordinator
{
    private static readonly ConcurrentDictionary<Guid, SemaphoreSlim> LocalLocks = new();

    public async Task<T> ExecuteAsync<T>(Guid projectId, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return await ExecuteLocalAsync(projectId, action, ct);
        T? result = default; var hasResult = false; var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
            if ((db.Database.ProviderName ?? string.Empty).Contains("Npgsql", StringComparison.OrdinalIgnoreCase))
                await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock(hashtext({projectId.ToString("D")}));", ct);
            result = await action(ct); hasResult = true; await tx.CommitAsync(ct);
        });
        return hasResult ? result! : throw new InvalidOperationException("Project mutation did not produce a result.");
    }

    public Task ExecuteAsync(Guid projectId, Func<CancellationToken, Task> action, CancellationToken ct) => ExecuteAsync(projectId, async token => { await action(token);
     return true;
     }, ct);

    private static async Task<T> ExecuteLocalAsync<T>(Guid projectId, Func<CancellationToken, Task<T>> action, CancellationToken ct)
    {
        var gate = LocalLocks.GetOrAdd(projectId, _ => new SemaphoreSlim(1, 1)); await gate.WaitAsync(ct);
        try { return await action(ct); } finally { gate.Release(); }
    }
}
