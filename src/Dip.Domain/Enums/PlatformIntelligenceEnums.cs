namespace Dip.Domain.Enums;

public enum DurableJobType
{
    Fuzz,
    Mutation,
    Soak,
    GitBisect,
    MediaRegression,
    Benchmark
}

public enum DurableJobStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    TimedOut
}

public enum IncidentStatus
{
    Open,
    Investigating,
    Mitigated,
    Resolved,
    Reopened
}

public enum IncidentEventType
{
    Observed,
    Reproduced,
    Symbolicated,
    RegressionLocated,
    RootCauseUpdated,
    ArtifactAttached,
    StatusChanged,
    BenchmarkRegression,
    Resolved,
    Reopened
}

public enum BenchmarkRunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    InfrastructureFailure
}

public enum BenchmarkVerdict
{
    NotComparable,
    Passed,
    Warning,
    Failed
}
