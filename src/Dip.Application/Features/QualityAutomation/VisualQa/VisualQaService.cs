// Purpose: Evaluates visual QA inputs, exposes the same deterministic comparator to autonomous QA, and converts differences into normalized evidence.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Artifacts;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.Evidence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityAutomation.VisualQa;

public sealed class VisualQaService
{
    public const string DefaultAssetRulesJson = """
    {
      "maya": {
        "polygonBudget": 500000,
        "missingTextureSeverity": "Critical",
        "unknownNodeSeverity": "Warning",
        "nonManifoldSeverity": "Critical",
        "ngonWarningCount": 100,
        "constructionHistoryWarningCount": 200,
        "unfrozenTransformSeverity": "Warning"
      },
      "unity": {
        "maxTextureDimension": 4096,
        "missingScriptSeverity": "Critical",
        "missingMaterialSeverity": "Critical",
        "requireLodForMeshes": false,
        "maxMeshVertices": 250000,
        "readWriteTextureSeverity": "Warning",
        "requireTextureCompression": true,
        "oversizedAudioClipMb": 64
      },
      "unreal": {
        "missingDependencySeverity": "Critical",
        "redirectorSeverity": "Warning",
        "blueprintCompileSeverity": "Critical",
        "maxTextureDimension": 8192,
        "naniteTriangleThreshold": 100000,
        "requireNaniteAboveThreshold": false,
        "unboundedMaterialInstructionWarning": 800
      }
    }
    """;

    private readonly IProjectRepository _projects;
    private readonly IQualityAutomationRepository _repository;
    private readonly ArtifactService _artifacts;
    private readonly EvidenceIngestionService _evidence;
    private readonly IVisualComparator _comparator;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly ProductionGraphProjectionService _graph;

    public VisualQaService(
        IProjectRepository projects,
        IQualityAutomationRepository repository,
        ArtifactService artifacts,
        EvidenceIngestionService evidence,
        IVisualComparator comparator,
        IUnitOfWork uow,
        ISystemClock clock,
        ProductionGraphProjectionService graph)
    {
        _projects = projects;
        _repository = repository;
        _artifacts = artifacts;
        _evidence = evidence;
        _comparator = comparator;
        _uow = uow;
        _clock = clock;
        _graph = graph;
    }

    /// <summary>
    /// Shared autonomous-QA bridge. P2 media regression normalizes arbitrary image
    /// formats to PNG and calls this method so durable Visual QA and autonomous
    /// VisualRegression use exactly the same pixel/SSIM implementation.
    /// </summary>
    public static async Task<VisualComparisonComputation> CompareAutonomousPngAsync(
        string baselinePng,
        string currentPng,
        IReadOnlyList<VisualMaskRegion>? masks,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(baselinePng) || string.IsNullOrWhiteSpace(currentPng))
            throw new ArgumentException("Autonomous visual comparison requires baseline and current PNG paths.");
        if (!File.Exists(baselinePng) || !File.Exists(currentPng))
            throw new FileNotFoundException("Autonomous visual comparison input is missing.");

        await using var baseline = new FileStream(baselinePng, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        await using var current = new FileStream(currentPng, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, true);
        return await new PortablePngVisualComparator().CompareAsync(
            baseline,
            current,
            masks ?? Array.Empty<VisualMaskRegion>(),
            ct);
    }

    /// <summary>
    /// Converts pixel and perceptual similarity into one bounded autonomous-media
    /// difference percentage without discarding either signal.
    /// </summary>
    public static decimal AutonomousDifferencePercent(VisualComparisonComputation computation)
    {
        if (!computation.Compatible)
            return 100m;
        var perceptualDifference = Math.Clamp((1m - computation.Ssim) * 100m, 0m, 100m);
        return decimal.Round(Math.Max(computation.ChangedPixelPercent, perceptualDifference), 4);
    }

    public async Task<QualityAutomationProfileDto> GetProfileAsync(Guid projectId, CancellationToken ct)
        => Map(await GetOrCreateProfileAsync(projectId, ct));

    public async Task<QualityAutomationProfileDto> UpdateProfileAsync(
        Guid projectId,
        UpdateQualityAutomationProfileCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateAssetRules(command.AssetRulesJson);
        var profile = await GetOrCreateProfileAsync(projectId, ct);
        profile.Update(
            command.Enabled,
            command.ChangedPixelWarningPercent,
            command.ChangedPixelFailurePercent,
            command.MinimumSsim,
            command.AssetRulesJson.Trim(),
            _clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
        return Map(profile);
    }

    public async Task<GoldenMasterProfileDto> RegisterGoldenAsync(
        Guid projectId,
        RegisterGoldenMasterCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureProjectAsync(projectId, ct);
        var captureKey = Required(command.CaptureKey, 500, nameof(command.CaptureKey));
        var opened = await _artifacts.OpenAsync(command.ArtifactRecordId, ct)
                     ?? throw new KeyNotFoundException($"Artifact {command.ArtifactRecordId} was not found.");
        if (opened.Record.ProjectId != projectId)
            throw new InvalidOperationException("Golden-master artifact belongs to another project.");
        if (!opened.Blob.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Visual comparison currently requires PNG golden artifacts.");

        await using var stream = opened.Content;
        var image = await PortablePngCodec.DecodeAsync(stream, ct);
        var masks = NormalizeMasks(command.Masks, image.Width, image.Height);
        var existing = await _repository.FindActiveGoldenAsync(projectId, captureKey, ct);
        existing?.Deactivate();

        var golden = new GoldenMasterProfile(
            Guid.NewGuid(),
            projectId,
            captureKey,
            command.ArtifactRecordId,
            Required(command.SceneKey, 300, nameof(command.SceneKey)),
            Required(command.CameraKey, 300, nameof(command.CameraKey)),
            image.Width,
            image.Height,
            JsonSerializer.Serialize(masks),
            true,
            Required(command.ApprovedBy, 300, nameof(command.ApprovedBy)),
            _clock.UtcNow);
        await _repository.AddGoldenAsync(golden, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(golden);
    }

    public async Task<IReadOnlyList<GoldenMasterProfileDto>> ListGoldensAsync(Guid projectId, CancellationToken ct)
    {
        await EnsureProjectAsync(projectId, ct);
        return (await _repository.ListGoldensAsync(projectId, ct)).Select(Map).ToArray();
    }

    public async Task<VisualComparisonDto> CompareAsync(
        Guid projectId,
        RunVisualComparisonCommand command,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(command);
        await EnsureProjectAsync(projectId, ct);
        var profile = await GetOrCreateProfileAsync(projectId, ct);
        if (!profile.Enabled) throw new InvalidOperationException("Quality automation is disabled for this project.");
        var captureKey = Required(command.CaptureKey, 500, nameof(command.CaptureKey));
        var golden = await _repository.FindActiveGoldenAsync(projectId, captureKey, ct)
                     ?? throw new KeyNotFoundException($"No active golden master exists for '{captureKey}'.");
        return await CompareCoreAsync(profile, golden, command.CurrentArtifactRecordId, ct);
    }

    public async Task<VisualComparisonDto?> TryAutoCompareAsync(
        Guid projectId,
        string captureKey,
        Guid currentArtifactRecordId,
        CancellationToken ct)
    {
        var profile = await GetOrCreateProfileAsync(projectId, ct);
        if (!profile.Enabled) return null;
        var golden = await _repository.FindActiveGoldenAsync(projectId, captureKey.Trim(), ct);
        return golden is null ? null : await CompareCoreAsync(profile, golden, currentArtifactRecordId, ct);
    }

    public async Task<IReadOnlyList<VisualComparisonDto>> ListComparisonsAsync(Guid projectId, int take, CancellationToken ct)
    {
        await EnsureProjectAsync(projectId, ct);
        return (await _repository.ListComparisonsAsync(projectId, Math.Clamp(take, 1, 500), ct))
            .Select(Map).ToArray();
    }

    private async Task<VisualComparisonDto> CompareCoreAsync(
        QualityAutomationProfile profile,
        GoldenMasterProfile golden,
        Guid currentArtifactRecordId,
        CancellationToken ct)
    {
        var baseline = await _artifacts.OpenAsync(golden.ArtifactRecordId, ct)
                       ?? throw new KeyNotFoundException($"Golden artifact {golden.ArtifactRecordId} was not found.");
        var current = await _artifacts.OpenAsync(currentArtifactRecordId, ct)
                      ?? throw new KeyNotFoundException($"Current artifact {currentArtifactRecordId} was not found.");
        if (baseline.Record.ProjectId != golden.ProjectId || current.Record.ProjectId != golden.ProjectId)
            throw new InvalidOperationException("Visual comparison artifacts must belong to the same project as the golden master.");

        await using var baselineStream = baseline.Content;
        await using var currentStream = current.Content;
        var masks = ParseMasks(golden.MaskJson);
        var computation = await _comparator.CompareAsync(baselineStream, currentStream, masks, ct);

        Guid? diffArtifactId = null;
        if (computation.Compatible && computation.DiffPng is { Length: > 0 } diff)
        {
            await using var diffStream = new MemoryStream(diff, writable: false);
            var diffRecord = await _artifacts.StoreAsync(
                golden.ProjectId,
                "visual-regression.diff",
                $"{SafeFileName(golden.CaptureKey)}-{_clock.UtcNow:yyyyMMddHHmmss}.png",
                diffStream,
                "image/png",
                new ArtifactLinkContext(),
                "qa-visual-diff",
                TimeSpan.FromDays(90),
                ct);
            diffArtifactId = diffRecord.Id;
        }

        var verdict = EvaluateVerdict(
            computation,
            profile.ChangedPixelWarningPercent,
            profile.ChangedPixelFailurePercent,
            profile.MinimumSsim);
        var summary = computation.Compatible
            ? $"{verdict}: {computation.ChangedPixelPercent:0.####}% pixels changed; tiled SSIM {computation.Ssim:0.######}."
            : $"Incompatible: {computation.IncompatibilityReason}";

        var record = new VisualComparisonRecord(
            Guid.NewGuid(), golden.ProjectId, golden.Id, golden.ArtifactRecordId,
            currentArtifactRecordId, diffArtifactId, verdict,
            computation.ChangedPixelPercent, computation.Ssim,
            computation.ChangedPixelCount, computation.EvaluatedPixelCount,
            summary, _clock.UtcNow);
        await _repository.AddComparisonAsync(record, ct);
        await _uow.SaveChangesAsync(ct);

        var outcome = verdict switch
        {
            VisualComparisonVerdict.Passed => EvidenceOutcome.Succeeded,
            VisualComparisonVerdict.Warning => EvidenceOutcome.Warning,
            VisualComparisonVerdict.Failed or VisualComparisonVerdict.Incompatible => EvidenceOutcome.Failed,
            _ => EvidenceOutcome.Unknown
        };
        await _evidence.IngestAsync(
            new IngestEvidenceCommand(
                golden.ProjectId,
                EvidenceSourceType.Quality,
                $"visual:{golden.Id:N}",
                record.Id.ToString("N"),
                EvidenceEventType.VisualRegression,
                $"visual:{golden.CaptureKey}"[..Math.Min(200, $"visual:{golden.CaptureKey}".Length)],
                "qa.visual-regression",
                $"Visual regression {verdict}: {golden.CaptureKey}",
                summary,
                outcome,
                EvidenceConfidence.Verified,
                record.ComparedAtUtc,
                SourceReference: diffArtifactId.HasValue ? $"/api/v1/artifacts/{diffArtifactId.Value}" : null,
                ArtifactId: currentArtifactRecordId.ToString("N"),
                PrivacyClass: EvidencePrivacyClass.ProjectRestricted,
                Metadata: JsonSerializer.SerializeToElement(new
                {
                    goldenMasterProfileId = golden.Id,
                    baselineArtifactRecordId = golden.ArtifactRecordId,
                    currentArtifactRecordId,
                    diffArtifactRecordId = diffArtifactId,
                    computation.ChangedPixelPercent,
                    computation.Ssim,
                    computation.ChangedPixelCount,
                    computation.EvaluatedPixelCount,
                    comparator = "portable-png-tiled-ssim-v2",
                    verdict = verdict.ToString()
                })),
            ct);
        await _graph.ProjectVisualComparisonAsync(
            golden.ProjectId, record.Id, golden.CaptureKey, verdict, currentArtifactRecordId, diffArtifactId, ct);
        return Map(record);
    }

    private static VisualComparisonVerdict EvaluateVerdict(
        VisualComparisonComputation computation,
        decimal changedPixelWarningPercent,
        decimal changedPixelFailurePercent,
        decimal minimumSsim)
        => !computation.Compatible
            ? VisualComparisonVerdict.Incompatible
            : computation.ChangedPixelPercent >= changedPixelFailurePercent || computation.Ssim < minimumSsim
                ? VisualComparisonVerdict.Failed
                : computation.ChangedPixelPercent >= changedPixelWarningPercent
                    ? VisualComparisonVerdict.Warning
                    : VisualComparisonVerdict.Passed;

    private async Task<QualityAutomationProfile> GetOrCreateProfileAsync(Guid projectId, CancellationToken ct)
    {
        await EnsureProjectAsync(projectId, ct);
        var existing = await _repository.GetProfileAsync(projectId, ct);
        if (existing is not null) return existing;
        var profile = new QualityAutomationProfile(
            Guid.NewGuid(), projectId, true, 0.5m, 3m, 0.97m, DefaultAssetRulesJson, _clock.UtcNow);
        await _repository.AddProfileAsync(profile, ct);
        await _uow.SaveChangesAsync(ct);
        return profile;
    }

    private async Task EnsureProjectAsync(Guid projectId, CancellationToken ct)
    {
        if (projectId == Guid.Empty) throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (await _projects.GetByIdAsync(projectId, ct) is null)
            throw new KeyNotFoundException($"Project {projectId} was not found.");
    }

    private static void ValidateAssetRules(string json)
    {
        if (string.IsNullOrWhiteSpace(json) || json.Length > 64000) throw new ArgumentException("AssetRulesJson must be 1-64000 characters.");
        try { using var _ = JsonDocument.Parse(json); }
        catch (JsonException ex) { throw new ArgumentException("AssetRulesJson must be valid JSON.", nameof(json), ex); }
    }

    private static IReadOnlyList<VisualMaskRegion> NormalizeMasks(IReadOnlyList<VisualMaskRegion>? masks, int width, int height)
    {
        var result = new List<VisualMaskRegion>();
        foreach (var mask in masks ?? Array.Empty<VisualMaskRegion>())
        {
            if (mask.X < 0 || mask.Y < 0 || mask.Width <= 0 || mask.Height <= 0 || mask.X + mask.Width > width || mask.Y + mask.Height > height)
                throw new ArgumentException("Visual mask must be a positive rectangle contained by the golden image.");
            result.Add(mask);
        }
        return result;
    }

    private static IReadOnlyList<VisualMaskRegion> ParseMasks(string json)
    {
        try { return JsonSerializer.Deserialize<VisualMaskRegion[]>(json) ?? Array.Empty<VisualMaskRegion>(); }
        catch (JsonException) { return Array.Empty<VisualMaskRegion>(); }
    }

    private static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = value.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var text = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(text) ? "visual-diff" : text[..Math.Min(80, text.Length)];
    }

    private static string Required(string? value, int max, string name)
    {
        var text = value?.Trim() ?? string.Empty;
        if (text.Length is < 1 || text.Length > max) throw new ArgumentException($"{name} must be 1-{max} characters.", name);
        return text;
    }

    private static QualityAutomationProfileDto Map(QualityAutomationProfile x)
        => new(x.Id, x.ProjectId, x.Enabled, x.ChangedPixelWarningPercent, x.ChangedPixelFailurePercent, x.MinimumSsim, x.AssetRulesJson, x.UpdatedAtUtc);

    private static GoldenMasterProfileDto Map(GoldenMasterProfile x)
        => new(x.Id, x.ProjectId, x.CaptureKey, x.ArtifactRecordId, x.SceneKey, x.CameraKey, x.Width, x.Height, ParseMasks(x.MaskJson), x.Active, x.ApprovedBy, x.ApprovedAtUtc);

    private static VisualComparisonDto Map(VisualComparisonRecord x)
        => new(x.Id, x.ProjectId, x.GoldenMasterProfileId, x.BaselineArtifactRecordId, x.CurrentArtifactRecordId, x.DiffArtifactRecordId, x.Verdict, x.ChangedPixelPercent, x.Ssim, x.ChangedPixelCount, x.EvaluatedPixelCount, x.Summary, x.ComparedAtUtc);
}
