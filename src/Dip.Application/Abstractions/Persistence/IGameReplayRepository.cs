// Purpose: Defines the IGameReplayRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IGameReplayRepository
{
    Task AddSessionAsync(GameReplaySession session, CancellationToken ct);
    Task<GameReplaySession?> GetSessionAsync(Guid id, CancellationToken ct);
    Task<GameReplaySession?> GetSessionByKeyAsync(Guid projectId, string key, CancellationToken ct);
    Task<IReadOnlyList<GameReplaySession>> ListSessionsAsync(Guid projectId, CancellationToken ct);
    Task AddRunAsync(GameReplayRun run, CancellationToken ct);
    Task<GameReplayRun?> GetRunAsync(Guid id, CancellationToken ct);
    Task<GameReplayRun?> FindBaselineRunAsync(Guid projectId, Guid sessionId, string hardwareFingerprint, string excludingCommit, CancellationToken ct);
    Task AddMetricsAsync(IEnumerable<GameReplayMetric> metrics, CancellationToken ct);
    Task<IReadOnlyList<GameReplayMetric>> ListMetricsAsync(Guid runId, CancellationToken ct);
}
