// Purpose: Coordinates P ol ic yC on tr ac ts application behavior for Policies.
using Dip.Domain.Enums;

namespace Dip.Application.Features.Policies;

public sealed record EvidencePolicySettings(
    bool? CollectMetadata = null,
    bool? CalculateContentHash = null,
    bool? AllowRawArtifactUpload = null);

public sealed record AgentPolicySettings(
    bool? MayaMonitoringEnabled = null,
    bool? UnityMonitoringEnabled = null,
    bool? UnrealMonitoringEnabled = null);

public sealed record RetentionPolicySettings(
    int? MetadataRetentionDays = null,
    int? ArtifactRetentionDays = null,
    int? VideoRetentionDays = null);

public sealed record AlertPolicySettings(
    int? CriticalThreshold = null,
    bool? NotifyOnContradiction = null);

public sealed record PrivacyPolicySettings(
    bool? AllowScreenshots = null,
    bool? AllowVideo = null,
    bool? HashOnly = null,
    IReadOnlyList<string>? ExcludedPaths = null);

public sealed record PolicyProfileDocument(
    string Name,
    bool Enabled,
    EvidencePolicySettings Evidence,
    AgentPolicySettings Agent,
    RetentionPolicySettings Retention,
    AlertPolicySettings Alert,
    PrivacyPolicySettings Privacy,
    Guid? QaPolicyId = null);

public sealed record PolicyProfileDto(
    Guid Id,
    PolicyScopeType ScopeType,
    Guid? ProjectId,
    string? AgentId,
    string Name,
    int Version,
    bool Enabled,
    PolicyProfileDocument Document,
    DateTimeOffset UpdatedAtUtc);

public sealed record EffectiveEvidencePolicy(
    bool CollectMetadata,
    bool CalculateContentHash,
    bool AllowRawArtifactUpload);

public sealed record EffectiveAgentPolicy(
    bool MayaMonitoringEnabled,
    bool UnityMonitoringEnabled,
    bool UnrealMonitoringEnabled);

public sealed record EffectiveRetentionPolicy(
    int MetadataRetentionDays,
    int ArtifactRetentionDays,
    int VideoRetentionDays);

public sealed record EffectiveAlertPolicy(
    int CriticalThreshold,
    bool NotifyOnContradiction);

public sealed record EffectivePrivacyPolicy(
    bool AllowScreenshots,
    bool AllowVideo,
    bool HashOnly,
    IReadOnlyList<string> ExcludedPaths);

public sealed record EffectivePolicy(
    EffectiveEvidencePolicy Evidence,
    EffectiveAgentPolicy Agent,
    EffectiveRetentionPolicy Retention,
    EffectiveAlertPolicy Alert,
    EffectivePrivacyPolicy Privacy,
    Guid? QaPolicyId,
    IReadOnlyList<Guid> AppliedProfileIds);
