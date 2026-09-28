// Purpose: Defines Project Forecast domain behavior and invariants for projects.
using Dip.Domain.Enums;
namespace Dip.Domain.Entities;
public sealed class ProjectForecast
{
    private ProjectForecast() { }
    public ProjectForecast(Guid id, Guid projectId, string forecastType, decimal riskPercent, DateTimeOffset? earliest, DateTimeOffset? latest, ForecastConfidence confidence, string rationaleJson, DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException(nameof(projectId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        ForecastType = forecastType.Trim();
        RiskPercent = Math.Clamp(decimal.Round(riskPercent, 2), 0, 100);
        EarliestUtc = earliest;
        LatestUtc = latest;
        Confidence = confidence;
        RationaleJson = rationaleJson.Length <= 32000 ? rationaleJson : rationaleJson[..32000];
        CreatedAtUtc = createdAtUtc;
    }
    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public string ForecastType { get; private set; } = string.Empty; public decimal RiskPercent { get; private set; }
    public DateTimeOffset? EarliestUtc { get; private set; }
    public DateTimeOffset? LatestUtc { get; private set; }
    public ForecastConfidence Confidence { get; private set; }
    public string RationaleJson { get; private set; } = string.Empty; public DateTimeOffset CreatedAtUtc { get; private set; }
}
