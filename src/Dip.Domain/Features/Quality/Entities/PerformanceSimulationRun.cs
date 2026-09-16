// Purpose: Represents P er fo rm an ce Si mu la ti on Ru n in the Quality domain.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class PerformanceSimulationRun
{
    private PerformanceSimulationRun() { }

    public PerformanceSimulationRun(
        Guid id,
        Guid projectId,
        Guid hardwareProfileId,
        string subjectKey,
        string artifactDirectory,
        DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (hardwareProfileId == Guid.Empty) throw new ArgumentException("HardwareProfileId is required.", nameof(hardwareProfileId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        HardwareProfileId = hardwareProfileId;
        SubjectKey = Required(subjectKey, 200, nameof(subjectKey));
        ArtifactDirectory = Required(artifactDirectory, 1000, nameof(artifactDirectory));
        Status = SimulationStatus.Queued;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid HardwareProfileId { get; private set; }
    public string SubjectKey { get; private set; } = string.Empty;
    public string ArtifactDirectory { get; private set; } = string.Empty;
    public SimulationStatus Status { get; private set; }
    public decimal ConfidencePercent { get; private set; }
    public string? ResultJson { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public DateTimeOffset? StartedAtUtc { get; private set; }
    public DateTimeOffset? FinishedAtUtc { get; private set; }

    public void Start(DateTimeOffset at)
    {
        if (Status != SimulationStatus.Queued) throw new InvalidOperationException($"Cannot start simulation from {Status}.");
        Status = SimulationStatus.Running;
        StartedAtUtc = at;
    }

    public void Complete(SimulationStatus status, decimal confidencePercent, string resultJson, DateTimeOffset at)
    {
        if (Status != SimulationStatus.Running) throw new InvalidOperationException($"Cannot complete simulation from {Status}.");
        if (status == SimulationStatus.Running || status == SimulationStatus.Queued) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        ConfidencePercent = Math.Clamp(confidencePercent, 0m, 100m);
        ResultJson = Required(resultJson, 32000, nameof(resultJson));
        FinishedAtUtc = at;
    }

    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
}
