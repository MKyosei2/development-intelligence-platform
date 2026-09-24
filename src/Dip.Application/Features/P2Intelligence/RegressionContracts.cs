// Purpose: Defines Git-bisect and metric-regression-origin commands, evaluations, and results.
namespace Dip.Application.Features.P2Intelligence;

public sealed record GitBisectCommand(
    ExperimentTarget Target,
    string GoodCommit,
    string BadCommit,
    int MaxEvaluations = 20);

public sealed record GitBisectEvaluation(
    string Commit,
    string Status,
    bool Passed,
    long DurationMs);

public sealed record GitBisectResult(
    string Status,
    string? FirstBadCommit,
    string? LastGoodCommit,
    int Evaluations,
    IReadOnlyList<GitBisectEvaluation> History,
    string ArtifactReference);

public sealed record RegressionPoint(
    string Revision,
    decimal Value,
    DateTimeOffset? ObservedAtUtc = null);

public sealed record RegressionOriginCommand(
    Guid ProjectId,
    string SubjectKey,
    IReadOnlyList<RegressionPoint> Points,
    bool LowerIsBetter,
    decimal ThresholdPercent = 10m);

public sealed record RegressionOriginResult(
    string Status,
    string? RegressionRevision,
    decimal BaselineValue,
    decimal RegressedValue,
    decimal ChangePercent,
    string Direction);
