// Purpose: Defines B en ch ma rk Ru n domain behavior and invariants for q ua li ty.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class BenchmarkRun
{
    private BenchmarkRun() { }
    public BenchmarkRun(Guid id, Guid projectId, Guid definitionId, Guid jobId, string subjectKey, string commitSha, string environmentFingerprint, string configurationHash, DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty || definitionId == Guid.Empty || jobId == Guid.Empty) throw new ArgumentException("Project, definition and job are required.");
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        BenchmarkDefinitionId = definitionId;
        JobId = jobId;
        SubjectKey = Req(subjectKey, 200);
        CommitSha = Req(commitSha, 64);
        EnvironmentFingerprint = Req(environmentFingerprint, 128);
        ConfigurationHash = Req(configurationHash, 64);
        Status = BenchmarkRunStatus.Queued;
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
    public Guid BenchmarkDefinitionId
    {
        get;
        private set;
    }
    public Guid JobId
    {
        get;
        private set;
    }
    public string SubjectKey
    {
        get;
        private set;
    } = string.Empty;
    public string CommitSha
    {
        get;
        private set;
    } = string.Empty;
    public string EnvironmentFingerprint
    {
        get;
        private set;
    } = string.Empty;
    public string ConfigurationHash
    {
        get;
        private set;
    } = string.Empty;
    public BenchmarkRunStatus Status
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
    public string? Summary
    {
        get;
        private set;
    }
    public void Start(DateTimeOffset at) { if (Status != BenchmarkRunStatus.Queued) throw new InvalidOperationException();
     Status = BenchmarkRunStatus.Running;
     StartedAtUtc = at;
     }
    public void Complete(BenchmarkRunStatus status, string summary, DateTimeOffset at)
    {
        if (Status != BenchmarkRunStatus.Running) throw new InvalidOperationException();
        Status = status;
        Summary = summary.Length <= 4000 ? summary : summary[..4000];
        FinishedAtUtc = at;
    }
    private static string Req(string? v, int max) { var s = v?.Trim() ?? string.Empty; if (s.Length is < 1 || s.Length > max) throw new ArgumentException(); return s; }
}
