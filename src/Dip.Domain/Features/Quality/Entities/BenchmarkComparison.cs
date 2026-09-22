// Purpose: Defines domain behavior and state for this feature area.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class BenchmarkComparison
{
    private BenchmarkComparison() { }
    public BenchmarkComparison(Guid id, Guid currentRunId, Guid baselineRunId, string metricName, decimal baselineValue, decimal currentValue, decimal differencePercent, BenchmarkVerdict verdict, string explanation)
    {
        if (currentRunId == Guid.Empty || baselineRunId == Guid.Empty) throw new ArgumentException();
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        CurrentRunId = currentRunId;
        BaselineRunId = baselineRunId;
        MetricName = Req(metricName, 160);
        BaselineValue = baselineValue;
        CurrentValue = currentValue;
        DifferencePercent = decimal.Round(differencePercent, 3);
        Verdict = verdict;
        Explanation = Req(explanation, 2000);
    }
    public Guid Id
    {
        get;
        private set;
    }
    public Guid CurrentRunId
    {
        get;
        private set;
    }
    public Guid BaselineRunId
    {
        get;
        private set;
    }
    public string MetricName
    {
        get;
        private set;
    } = string.Empty;
    public decimal BaselineValue
    {
        get;
        private set;
    }
    public decimal CurrentValue
    {
        get;
        private set;
    }
    public decimal DifferencePercent
    {
        get;
        private set;
    }
    public BenchmarkVerdict Verdict
    {
        get;
        private set;
    }
    public string Explanation
    {
        get;
        private set;
    } = string.Empty;
    private static string Req(string? v, int max) { var s = v?.Trim() ?? string.Empty; if (s.Length is < 1 || s.Length > max) throw new ArgumentException(); return s; }
}
