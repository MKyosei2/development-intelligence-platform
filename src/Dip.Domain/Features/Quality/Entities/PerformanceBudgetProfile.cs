// Purpose: Defines Performance Budget Profile domain behavior and invariants for quality.
namespace Dip.Domain.Entities;
public sealed class PerformanceBudgetProfile
{
    private PerformanceBudgetProfile() { }
    public PerformanceBudgetProfile(Guid id, Guid? projectId, string key, string name, string platform, string targetProfile, string rulesJson, bool enabled, DateTimeOffset createdAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId cannot be empty.", nameof(projectId));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        Key = Required(key, 120, nameof(key));
        Name = Required(name, 200, nameof(name));
        Platform = Required(platform, 80, nameof(platform));
        TargetProfile = Required(targetProfile, 120, nameof(targetProfile));
        RulesJson = Required(rulesJson, 32000, nameof(rulesJson));
        Enabled = enabled;
        CreatedAtUtc = createdAtUtc;
    }
    public Guid Id
    {
        get;
        private set;
    }
    public Guid? ProjectId
    {
        get;
        private set;
    }
    public string Key
    {
        get;
        private set;
    } = string.Empty;
    public string Name
    {
        get;
        private set;
    } = string.Empty;
    public string Platform
    {
        get;
        private set;
    } = string.Empty;
    public string TargetProfile
    {
        get;
        private set;
    } = string.Empty;
    public string RulesJson
    {
        get;
        private set;
    } = "[]";
    public bool Enabled
    {
        get;
        private set;
    }
    public DateTimeOffset CreatedAtUtc
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
