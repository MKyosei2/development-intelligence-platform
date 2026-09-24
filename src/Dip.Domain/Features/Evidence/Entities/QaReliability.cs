// Purpose: Defines QA test reliability classification and regression-baseline state transitions.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class QaTestReliability
{
    private QaTestReliability() { }

    public QaTestReliability(
        Guid id,
        Guid projectId,
        string testKey,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId == Guid.Empty
            ? throw new ArgumentException(nameof(projectId))
            : projectId;
        TestKey = Text(testKey, 500);
        Classification = TestReliabilityClassification.Unknown;
        FirstObservedAtUtc = LastObservedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string TestKey { get; private set; } = string.Empty;
    public int PassCount { get; private set; }
    public int FailCount { get; private set; }
    public int ConsecutivePasses { get; private set; }
    public int ConsecutiveFailures { get; private set; }
    public TestReliabilityClassification Classification { get; private set; }
    public string? LastCommitSha { get; private set; }
    public DateTimeOffset FirstObservedAtUtc { get; private set; }
    public DateTimeOffset LastObservedAtUtc { get; private set; }

    public decimal FailureRatePercent =>
        PassCount + FailCount == 0
            ? 0m
            : decimal.Round(
                (decimal)FailCount / (PassCount + FailCount) * 100m,
                2);

    public void Observe(
        bool passed,
        bool infrastructureFailure,
        string? commitSha,
        DateTimeOffset now)
    {
        LastCommitSha = string.IsNullOrWhiteSpace(commitSha)
            ? null
            : commitSha.Trim();
        LastObservedAtUtc = now;

        if (infrastructureFailure)
        {
            Classification = TestReliabilityClassification.EnvironmentFailure;
            return;
        }

        if (passed)
        {
            PassCount++;
            ConsecutivePasses++;
            ConsecutiveFailures = 0;
        }
        else
        {
            FailCount++;
            ConsecutiveFailures++;
            ConsecutivePasses = 0;
        }

        var total = PassCount + FailCount;
        Classification = total < 3
            ? TestReliabilityClassification.Unknown
            : FailCount == 0
                ? TestReliabilityClassification.StablePass
                : PassCount == 0 && FailCount >= 3
                    ? TestReliabilityClassification.StableFail
                    : FailureRatePercent is >= 5m and <= 95m
                        ? TestReliabilityClassification.Flaky
                        : FailureRatePercent > 95m
                            ? TestReliabilityClassification.StableFail
                            : TestReliabilityClassification.StablePass;
    }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}

public sealed class QaRegressionBaseline
{
    private QaRegressionBaseline() { }

    public QaRegressionBaseline(
        Guid id,
        Guid projectId,
        string subjectKey,
        string metricKey,
        string commitSha,
        decimal value,
        DateTimeOffset now)
    {
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        SubjectKey = Text(subjectKey, 200);
        MetricKey = Text(metricKey, 200);
        BaselineCommit = Text(commitSha, 64);
        LastKnownGoodCommit = BaselineCommit;
        BaselineValue = value;
        CurrentValue = value;
        Status = QaRegressionStatus.Baseline;
        UpdatedAtUtc = now;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string SubjectKey { get; private set; } = string.Empty;
    public string MetricKey { get; private set; } = string.Empty;
    public string BaselineCommit { get; private set; } = string.Empty;
    public string LastKnownGoodCommit { get; private set; } = string.Empty;
    public string? FirstBadCommit { get; private set; }
    public decimal BaselineValue { get; private set; }
    public decimal CurrentValue { get; private set; }
    public decimal RegressionPercent { get; private set; }
    public QaRegressionStatus Status { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Observe(
        string commitSha,
        decimal value,
        bool healthy,
        DateTimeOffset now)
    {
        CurrentValue = value;
        RegressionPercent = BaselineValue == 0
            ? value <= 0 ? 0m : 100m
            : decimal.Round(
                (value - BaselineValue) / Math.Abs(BaselineValue) * 100m,
                2);

        if (healthy)
        {
            LastKnownGoodCommit = Text(commitSha, 64);
            Status = Status == QaRegressionStatus.Regressed
                ? QaRegressionStatus.Recovered
                : QaRegressionStatus.Healthy;
            FirstBadCommit = null;
        }
        else
        {
            FirstBadCommit ??= Text(commitSha, 64);
            Status = QaRegressionStatus.Regressed;
        }

        UpdatedAtUtc = now;
    }

    public void Rebaseline(
        string commitSha,
        decimal value,
        DateTimeOffset now)
    {
        BaselineCommit = Text(commitSha, 64);
        LastKnownGoodCommit = BaselineCommit;
        FirstBadCommit = null;
        BaselineValue = CurrentValue = value;
        RegressionPercent = 0;
        Status = QaRegressionStatus.Baseline;
        UpdatedAtUtc = now;
    }

    private static string Text(string? value, int maximumLength)
    {
        var normalized = value?.Trim() ?? string.Empty;
        if (normalized.Length < 1 || normalized.Length > maximumLength)
            throw new ArgumentException();
        return normalized;
    }
}
