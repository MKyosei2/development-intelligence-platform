// Purpose: Defines Game Replay Run domain behavior and invariants for quality.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;

public sealed class GameReplayRun
{
    private GameReplayRun() { }
    public GameReplayRun(Guid id, Guid projectId, Guid sessionId, Guid jobId, string commitSha, string buildId, string hardwareFingerprint, DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty || sessionId == Guid.Empty || jobId == Guid.Empty) throw new ArgumentException("Project, session and job IDs are required.");
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        SessionId = sessionId;
        JobId = jobId;
        CommitSha = Required(commitSha, 64, nameof(commitSha));
        BuildId = Required(buildId, 200, nameof(buildId));
        HardwareFingerprint = Required(hardwareFingerprint, 256, nameof(hardwareFingerprint));
        Status = GameReplayRunStatus.Queued;
        CreatedAtUtc = createdAtUtc;
    }
    public Guid Id
    {
        get;
        private set;
    }
    public Guid ProjectId
    {
        get;
        private set;
    }
    public Guid SessionId
    {
        get;
        private set;
    }
    public Guid JobId
    {
        get;
        private set;
    }
    public string CommitSha
    {
        get;
        private set;
    } = string.Empty;
    public string BuildId
    {
        get;
        private set;
    } = string.Empty;
    public string HardwareFingerprint
    {
        get;
        private set;
    } = string.Empty;
    public GameReplayRunStatus Status
    {
        get;
        private set;
    }
    public string? SummaryJson
    {
        get;
        private set;
    }
    public DateTimeOffset CreatedAtUtc
    {
        get;
        private set;
    }
    public DateTimeOffset? StartedAtUtc
    {
        get;
        private set;
    }
    public DateTimeOffset? FinishedAtUtc
    {
        get;
        private set;
    }
    public void Start(DateTimeOffset at)
    {
        Status = GameReplayRunStatus.Running;
        StartedAtUtc = at;
    }
    public void Complete(bool success, string summaryJson, DateTimeOffset at)
    {
        Status = success ? GameReplayRunStatus.Succeeded : GameReplayRunStatus.Failed;
        SummaryJson = summaryJson.Length <= 32000 ? summaryJson : summaryJson[..32000];
        FinishedAtUtc = at;
    }
    private static string Required(string? value, int max, string name)
    {
        var v = value?.Trim() ?? string.Empty;
        if (v.Length is < 1 || v.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return v;
    }
}
