// Purpose: Defines Benchmark Metric domain behavior and invariants for quality.
namespace Dip.Domain.Entities;
public sealed class BenchmarkMetric
{
    private BenchmarkMetric() { }
    public BenchmarkMetric(Guid id, Guid benchmarkRunId, string name, decimal value, string unit, string aggregation)
    {
        if (benchmarkRunId == Guid.Empty) throw new ArgumentException();
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        BenchmarkRunId = benchmarkRunId;
        Name = Req(name, 160);
        Value = value;
        Unit = Req(unit, 40);
        Aggregation = Req(aggregation, 40);
    }
    public Guid Id
    {
        get;
        private set;
    }
    public Guid BenchmarkRunId
    {
        get;
        private set;
    }
    public string Name
    {
        get;
        private set;
    } = string.Empty;
    public decimal Value
    {
        get;
        private set;
    }
    public string Unit
    {
        get;
        private set;
    } = string.Empty;
    public string Aggregation
    {
        get;
        private set;
    } = string.Empty;
    private static string Req(string? v, int max) { var s = v?.Trim() ?? string.Empty; if (s.Length is < 1 || s.Length > max) throw new ArgumentException(); return s; }
}
