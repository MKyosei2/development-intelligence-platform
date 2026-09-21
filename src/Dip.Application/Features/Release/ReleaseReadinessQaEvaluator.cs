// Purpose: Evaluates QA governance verdicts, overrides, provenance freshness, and exact revision binding for release readiness.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Release;

internal sealed class ReleaseReadinessQaEvaluator(
    IQaGovernanceRepository qaGovernance,
    ISystemClock clock)
{
    public async Task EvaluateAsync(ReleaseReadinessEvaluationContext context, CancellationToken ct)
    {
        var request = context.Request;
        var activePolicy = await qaGovernance.GetActivePolicyAsync(request.ProjectId, ct);
        var policyValid = TryParseQaPolicyRules(activePolicy, out var rules);
        if (activePolicy is not null && !policyValid)
        {
            context.Blockers.Add("The active QA policy contains invalid JSON and cannot be trusted.");
            context.Actions.Add("Repair the active QA policy and rerun QA governance before release.");
        }

        var gate = await qaGovernance.GetLatestPolicyEvaluationAsync(request.ProjectId, ct);
        var overrides = await qaGovernance.ListReleaseOverridesAsync(request.ProjectId, 100, ct);
        var effectiveOverride = gate is null
            ? null
            : overrides.FirstOrDefault(x =>
                x.IsEffective(clock.UtcNow)
                && x.PolicyEvaluationId == gate.Id
                && string.Equals(x.CommitSha, request.CommitSha, StringComparison.OrdinalIgnoreCase));

        if (gate is null)
        {
            context.Warnings.Add("No QA governance policy evaluation exists for this project.");
            context.Actions.Add("Run QA with the governance pipeline before release.");
        }
        else if (gate.Verdict == QaGateVerdict.NoGo && effectiveOverride is null)
        {
            context.Blockers.Add($"QA policy gate is NO-GO: {gate.Summary}");
            context.Actions.Add("Resolve the blockers or obtain an owner-approved, commit-bound, unexpired release override.");
        }
        else if (gate.Verdict == QaGateVerdict.NoGo && effectiveOverride is not null)
        {
            context.Warnings.Add(
                $"QA NO-GO is temporarily overridden by {effectiveOverride.ApprovedBy}; override expires {effectiveOverride.ExpiresAtUtc:O}. Original gate: {gate.Summary}");
            context.Actions.Add("Review the accepted risk before the override expires and resolve the underlying QA blocker.");
        }
        else if (gate.Verdict == QaGateVerdict.ReviewRequired)
        {
            context.Warnings.Add($"QA policy gate requires review: {gate.Summary}");
        }
        else if (gate.Verdict == QaGateVerdict.Unavailable)
        {
            context.Blockers.Add("QA policy gate is unavailable; release quality cannot be verified.");
        }

        var maxAgeDays = Math.Clamp(
            activePolicy is null ? request.EvidenceFreshnessDays : rules.MaxEvidenceAgeDays,
            1,
            90);
        var provenance = await qaGovernance.GetLatestProvenanceAsync(request.ProjectId, ct);
        if (provenance is null)
        {
            context.Blockers.Add("No QA provenance exists for the release candidate.");
            context.Actions.Add("Run QA on the candidate commit to capture provenance.");
            return;
        }

        var age = (clock.UtcNow - provenance.CapturedAtUtc).TotalDays;
        if (age > maxAgeDays)
        {
            var message = $"Latest QA provenance is stale ({age:0.#} days old; policy maximum {maxAgeDays} days).";
            if (activePolicy is null || !policyValid || rules.BlockOnStaleEvidence)
                context.Blockers.Add(message);
            else
                context.Warnings.Add(message);
            context.Actions.Add("Rerun QA on the release candidate to refresh exact-revision evidence.");
        }

        if (string.IsNullOrWhiteSpace(provenance.CommitSha))
        {
            context.Blockers.Add("Latest QA provenance does not identify an exact commit.");
            context.Actions.Add("Run QA against an immutable release-candidate commit SHA.");
        }
        else if (!string.Equals(provenance.CommitSha, request.CommitSha, StringComparison.OrdinalIgnoreCase))
        {
            context.Blockers.Add("Latest QA provenance belongs to a different commit than the release candidate.");
            context.Actions.Add("Run QA against the exact candidate commit.");
        }
    }

    private static bool TryParseQaPolicyRules(QaPolicy? policy, out QaPolicyRules rules)
    {
        if (policy is null)
        {
            rules = new QaPolicyRules();
            return true;
        }

        try
        {
            rules = JsonSerializer.Deserialize<QaPolicyRules>(policy.RulesJson) ?? new QaPolicyRules();
            return true;
        }
        catch (JsonException)
        {
            rules = new QaPolicyRules();
            return false;
        }
    }
}
