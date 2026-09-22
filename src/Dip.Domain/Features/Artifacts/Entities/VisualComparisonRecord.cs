// Purpose: Defines domain behavior and state for this feature area.
using Dip.Domain.Enums;

namespace Dip.Domain.Entities;

public sealed class VisualComparisonRecord
{
    private VisualComparisonRecord() { }

    public VisualComparisonRecord(
        Guid id,
        Guid projectId,
        Guid goldenMasterProfileId,
        Guid baselineArtifactRecordId,
        Guid currentArtifactRecordId,
        Guid? diffArtifactRecordId,
        VisualComparisonVerdict verdict,
        decimal changedPixelPercent,
        decimal ssim,
        int changedPixelCount,
        int evaluatedPixelCount,
        string summary,
        DateTimeOffset comparedAtUtc)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (goldenMasterProfileId == Guid.Empty) throw new ArgumentException("GoldenMasterProfileId is required.", nameof(goldenMasterProfileId));
        if (baselineArtifactRecordId == Guid.Empty) throw new ArgumentException("BaselineArtifactRecordId is required.", nameof(baselineArtifactRecordId));
        if (currentArtifactRecordId == Guid.Empty) throw new ArgumentException("CurrentArtifactRecordId is required.", nameof(currentArtifactRecordId));
        if (!Enum.IsDefined(verdict)) throw new ArgumentOutOfRangeException(nameof(verdict));
        if (changedPixelPercent is < 0m or > 100m) throw new ArgumentOutOfRangeException(nameof(changedPixelPercent));
        if (ssim is < 0m or > 1m) throw new ArgumentOutOfRangeException(nameof(ssim));
        if (changedPixelCount < 0 || evaluatedPixelCount < 0) throw new ArgumentOutOfRangeException(nameof(changedPixelCount));
        Id = id == Guid.Empty ? Guid.NewGuid() : id;
        ProjectId = projectId;
        GoldenMasterProfileId = goldenMasterProfileId;
        BaselineArtifactRecordId = baselineArtifactRecordId;
        CurrentArtifactRecordId = currentArtifactRecordId;
        DiffArtifactRecordId = diffArtifactRecordId;
        Verdict = verdict;
        ChangedPixelPercent = changedPixelPercent;
        Ssim = ssim;
        ChangedPixelCount = changedPixelCount;
        EvaluatedPixelCount = evaluatedPixelCount;
        Summary = Required(summary, 2000, nameof(summary));
        ComparedAtUtc = comparedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ProjectId { get; private set; }
    public Guid GoldenMasterProfileId { get; private set; }
    public Guid BaselineArtifactRecordId { get; private set; }
    public Guid CurrentArtifactRecordId { get; private set; }
    public Guid? DiffArtifactRecordId { get; private set; }
    public VisualComparisonVerdict Verdict { get; private set; }
    public decimal ChangedPixelPercent { get; private set; }
    public decimal Ssim { get; private set; }
    public int ChangedPixelCount { get; private set; }
    public int EvaluatedPixelCount { get; private set; }
    public string Summary { get; private set; } = string.Empty;
    public DateTimeOffset ComparedAtUtc { get; private set; }

    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }
}
