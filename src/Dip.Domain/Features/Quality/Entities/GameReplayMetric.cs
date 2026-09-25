// Purpose: Defines Game Replay Metric domain behavior and invariants for quality.
namespace Dip.Domain.Entities;
public sealed class GameReplayMetric
{
    private GameReplayMetric() { }
    public GameReplayMetric(Guid id, Guid replayRunId, string name, decimal value, string unit, string aggregation)
    {
        if (replayRunId == Guid.Empty) throw new ArgumentException("ReplayRunId is required.", nameof(replayRunId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ReplayRunId = replayRunId;
        Name = Required(name, 160, nameof(name));
        Value = value;
        Unit = Required(unit, 40, nameof(unit));
        Aggregation = Required(aggregation, 40, nameof(aggregation));
    }
    public Guid Id
    {
        get;
        private set;
    }
    public Guid ReplayRunId
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
    private static string Required(string? value, int max, string name)
    {
        var v = value?.Trim() ?? string.Empty;
        if (v.Length is < 1 || v.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return v;
    }
}
