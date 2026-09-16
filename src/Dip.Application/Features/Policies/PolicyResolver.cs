// Purpose: Coordinates P ol ic yR es ol ve r application behavior for Policies.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;

namespace Dip.Application.Features.Policies;

public sealed class PolicyResolver
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPolicyProfileRepository _profiles;

    public PolicyResolver(IPolicyProfileRepository profiles)
        => _profiles = profiles;

    public async Task<EffectivePolicy> ResolveAsync(
        Guid projectId,
        string? agentId,
        CancellationToken ct)
    {
        var effective = SecureDefaults();
        var applied = new List<Guid>();

        var global = await _profiles.GetGlobalAsync(ct);
        if (global is { Enabled: true })
        {
            effective = ApplyGlobal(effective, Read(global));
            applied.Add(global.Id);
        }

        if (projectId != Guid.Empty)
        {
            var project = await _profiles.GetProjectAsync(projectId, ct);
            if (project is { Enabled: true })
            {
                effective = ApplyRestriction(effective, Read(project));
                applied.Add(project.Id);
            }
        }

        if (!string.IsNullOrWhiteSpace(agentId))
        {
            var agent = await _profiles.GetAgentAsync(agentId, ct);
            if (agent is { Enabled: true })
            {
                effective = ApplyRestriction(effective, Read(agent));
                applied.Add(agent.Id);
            }
        }

        return effective with { AppliedProfileIds = applied };
    }

    public static EffectivePolicy SecureDefaults() =>
        new(
            new EffectiveEvidencePolicy(
                CollectMetadata: true,
                CalculateContentHash: true,
                AllowRawArtifactUpload: false),
            new EffectiveAgentPolicy(
                MayaMonitoringEnabled: true,
                UnityMonitoringEnabled: true,
                UnrealMonitoringEnabled: true),
            new EffectiveRetentionPolicy(
                MetadataRetentionDays: 365,
                ArtifactRetentionDays: 90,
                VideoRetentionDays: 30),
            new EffectiveAlertPolicy(
                CriticalThreshold: 1,
                NotifyOnContradiction: true),
            new EffectivePrivacyPolicy(
                AllowScreenshots: false,
                AllowVideo: false,
                HashOnly: true,
                ExcludedPaths: Array.Empty<string>()),
            QaPolicyId: null,
            AppliedProfileIds: Array.Empty<Guid>());

    /// <summary>
    /// Global is the administrator-controlled ceiling/default and may intentionally
    /// be more permissive than SecureDefaults.
    /// </summary>
    public static EffectivePolicy ApplyGlobal(
        EffectivePolicy current,
        PolicyProfileDocument global)
    {
        return new EffectivePolicy(
            new EffectiveEvidencePolicy(
                global.Evidence.CollectMetadata ?? current.Evidence.CollectMetadata,
                global.Evidence.CalculateContentHash ?? current.Evidence.CalculateContentHash,
                global.Evidence.AllowRawArtifactUpload ?? current.Evidence.AllowRawArtifactUpload),
            new EffectiveAgentPolicy(
                global.Agent.MayaMonitoringEnabled ?? current.Agent.MayaMonitoringEnabled,
                global.Agent.UnityMonitoringEnabled ?? current.Agent.UnityMonitoringEnabled,
                global.Agent.UnrealMonitoringEnabled ?? current.Agent.UnrealMonitoringEnabled),
            new EffectiveRetentionPolicy(
                Positive(global.Retention.MetadataRetentionDays, current.Retention.MetadataRetentionDays),
                Positive(global.Retention.ArtifactRetentionDays, current.Retention.ArtifactRetentionDays),
                Positive(global.Retention.VideoRetentionDays, current.Retention.VideoRetentionDays)),
            new EffectiveAlertPolicy(
                Positive(global.Alert.CriticalThreshold, current.Alert.CriticalThreshold),
                global.Alert.NotifyOnContradiction ?? current.Alert.NotifyOnContradiction),
            new EffectivePrivacyPolicy(
                global.Privacy.AllowScreenshots ?? current.Privacy.AllowScreenshots,
                global.Privacy.AllowVideo ?? current.Privacy.AllowVideo,
                global.Privacy.HashOnly ?? current.Privacy.HashOnly,
                NormalizePaths(global.Privacy.ExcludedPaths ?? current.Privacy.ExcludedPaths)),
            global.QaPolicyId ?? current.QaPolicyId,
            current.AppliedProfileIds);
    }

    /// <summary>
    /// Project/Agent overrides can only restrict collection/privacy ceilings.
    /// Retention is capped with Min; excluded paths are unioned.
    /// </summary>
    public static EffectivePolicy ApplyRestriction(
        EffectivePolicy parent,
        PolicyProfileDocument child)
    {
        return new EffectivePolicy(
            new EffectiveEvidencePolicy(
                parent.Evidence.CollectMetadata &&
                    (child.Evidence.CollectMetadata ?? true),
                parent.Evidence.CalculateContentHash &&
                    (child.Evidence.CalculateContentHash ?? true),
                parent.Evidence.AllowRawArtifactUpload &&
                    (child.Evidence.AllowRawArtifactUpload ?? true)),
            new EffectiveAgentPolicy(
                parent.Agent.MayaMonitoringEnabled &&
                    (child.Agent.MayaMonitoringEnabled ?? true),
                parent.Agent.UnityMonitoringEnabled &&
                    (child.Agent.UnityMonitoringEnabled ?? true),
                parent.Agent.UnrealMonitoringEnabled &&
                    (child.Agent.UnrealMonitoringEnabled ?? true)),
            new EffectiveRetentionPolicy(
                Math.Min(
                    parent.Retention.MetadataRetentionDays,
                    Positive(child.Retention.MetadataRetentionDays,
                        parent.Retention.MetadataRetentionDays)),
                Math.Min(
                    parent.Retention.ArtifactRetentionDays,
                    Positive(child.Retention.ArtifactRetentionDays,
                        parent.Retention.ArtifactRetentionDays)),
                Math.Min(
                    parent.Retention.VideoRetentionDays,
                    Positive(child.Retention.VideoRetentionDays,
                        parent.Retention.VideoRetentionDays))),
            new EffectiveAlertPolicy(
                Math.Min(
                    parent.Alert.CriticalThreshold,
                    Positive(child.Alert.CriticalThreshold,
                        parent.Alert.CriticalThreshold)),
                child.Alert.NotifyOnContradiction ??
                    parent.Alert.NotifyOnContradiction),
            new EffectivePrivacyPolicy(
                parent.Privacy.AllowScreenshots &&
                    (child.Privacy.AllowScreenshots ?? true),
                parent.Privacy.AllowVideo &&
                    (child.Privacy.AllowVideo ?? true),
                parent.Privacy.HashOnly ||
                    (child.Privacy.HashOnly ?? false),
                NormalizePaths(
                    parent.Privacy.ExcludedPaths
                        .Concat(child.Privacy.ExcludedPaths ?? Array.Empty<string>())
                        .ToArray())),
            child.QaPolicyId ?? parent.QaPolicyId,
            parent.AppliedProfileIds);
    }

    internal static PolicyProfileDocument Read(PolicyProfile profile)
    {
        return new PolicyProfileDocument(
            profile.Name,
            profile.Enabled,
            Parse<EvidencePolicySettings>(profile.EvidencePolicyJson),
            Parse<AgentPolicySettings>(profile.AgentPolicyJson),
            Parse<RetentionPolicySettings>(profile.RetentionPolicyJson),
            Parse<AlertPolicySettings>(profile.AlertPolicyJson),
            Parse<PrivacyPolicySettings>(profile.PrivacyPolicyJson),
            profile.QaPolicyId);
    }

    private static T Parse<T>(string json) where T : class
        => JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException(
                $"Policy JSON for {typeof(T).Name} could not be parsed.");

    private static int Positive(int? value, int fallback)
        => value is > 0 ? value.Value : fallback;

    private static IReadOnlyList<string> NormalizePaths(
        IEnumerable<string> values)
        => values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim().Replace('\\', '/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(value => value, StringComparer.OrdinalIgnoreCase)
            .ToArray();
}
