namespace Dip.Domain.Entities;

public sealed class QualityAutomationProfile
{
    private QualityAutomationProfile() { }

    public QualityAutomationProfile(
        Guid id,
        Guid projectId,
        bool enabled,
        decimal changedPixelWarningPercent,
        decimal changedPixelFailurePercent,
        decimal minimumSsim,
        string assetRulesJson,
        DateTimeOffset updatedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        ValidatePercent(changedPixelWarningPercent, nameof(changedPixelWarningPercent));
        ValidatePercent(changedPixelFailurePercent, nameof(changedPixelFailurePercent));
        if (changedPixelFailurePercent < changedPixelWarningPercent)
            throw new ArgumentException("Failure pixel threshold must be greater than or equal to warning threshold.");
        if (minimumSsim is < 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(minimumSsim));
        if (string.IsNullOrWhiteSpace(assetRulesJson) || assetRulesJson.Length > 64000)
            throw new ArgumentException("AssetRulesJson must be 1-64000 characters.", nameof(assetRulesJson));

        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        Enabled = enabled;
        ChangedPixelWarningPercent = changedPixelWarningPercent;
        ChangedPixelFailurePercent = changedPixelFailurePercent;
        MinimumSsim = minimumSsim;
        AssetRulesJson = assetRulesJson;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public bool Enabled { get; private set; }
    public decimal ChangedPixelWarningPercent { get; private set; }
    public decimal ChangedPixelFailurePercent { get; private set; }
    public decimal MinimumSsim { get; private set; }
    public string AssetRulesJson { get; private set; } = "{}";
    public DateTimeOffset UpdatedAtUtc { get; private set; }

    public void Update(
        bool enabled,
        decimal changedPixelWarningPercent,
        decimal changedPixelFailurePercent,
        decimal minimumSsim,
        string assetRulesJson,
        DateTimeOffset now)
    {
        ValidatePercent(changedPixelWarningPercent, nameof(changedPixelWarningPercent));
        ValidatePercent(changedPixelFailurePercent, nameof(changedPixelFailurePercent));
        if (changedPixelFailurePercent < changedPixelWarningPercent)
            throw new ArgumentException("Failure pixel threshold must be greater than or equal to warning threshold.");
        if (minimumSsim is < 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(minimumSsim));
        if (string.IsNullOrWhiteSpace(assetRulesJson) || assetRulesJson.Length > 64000)
            throw new ArgumentException("AssetRulesJson must be 1-64000 characters.", nameof(assetRulesJson));
        Enabled = enabled;
        ChangedPixelWarningPercent = changedPixelWarningPercent;
        ChangedPixelFailurePercent = changedPixelFailurePercent;
        MinimumSsim = minimumSsim;
        AssetRulesJson = assetRulesJson;
        UpdatedAtUtc = now;
    }

    private static void ValidatePercent(decimal value, string name)
    {
        if (value is < 0m or > 100m) throw new ArgumentOutOfRangeException(name);
    }
}
