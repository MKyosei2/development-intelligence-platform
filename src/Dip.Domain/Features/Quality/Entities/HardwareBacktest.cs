// Purpose: Defines domain behavior and state for this feature area.
namespace Dip.Domain.Entities;
public sealed class HardwareBacktest
{
    private HardwareBacktest() { }
    public HardwareBacktest(Guid id, Guid projectId, Guid hardwareProfileId, string scenarioKey, string commitSha, decimal predictedFps, decimal actualFps, string environmentFingerprint, DateTimeOffset measuredAtUtc)
    {
        if (projectId == Guid.Empty || hardwareProfileId == Guid.Empty) throw new ArgumentException("Project and hardware profile IDs are required.");
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        HardwareProfileId = hardwareProfileId;
        ScenarioKey = Required(scenarioKey, 200, nameof(scenarioKey));
        CommitSha = Required(commitSha, 64, nameof(commitSha));
        PredictedFps = predictedFps;
        ActualFps = actualFps;
        PredictionErrorPercent = predictedFps == 0 ? 0 : (actualFps - predictedFps) / predictedFps * 100m;
        EnvironmentFingerprint = Required(environmentFingerprint, 256, nameof(environmentFingerprint));
        MeasuredAtUtc = measuredAtUtc;
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
    public Guid HardwareProfileId
    {
        get;
        private set;
    }
    public string ScenarioKey
    {
        get;
        private set;
    } = string.Empty;
    public string CommitSha
    {
        get;
        private set;
    } = string.Empty;
    public decimal PredictedFps
    {
        get;
        private set;
    }
    public decimal ActualFps
    {
        get;
        private set;
    }
    public decimal PredictionErrorPercent
    {
        get;
        private set;
    }
    public string EnvironmentFingerprint
    {
        get;
        private set;
    } = string.Empty;
    public DateTimeOffset MeasuredAtUtc
    {
        get;
        private set;
    }
    private static string Required(string? value, int max, string name)
    {
        var v = value?.Trim() ?? string.Empty;
        if (v.Length is < 1 || v.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return v;
    }
}
