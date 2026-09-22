// Purpose: Parses QA policy rules and evaluates completed runs without persistence side effects.
using System.Text.Json;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

public sealed class QaPolicyEvaluator
{
    public QaPolicyRules ReadRules(QaPolicy? policy)
    {
        if (policy is null)
            return new();

        try
        {
            return JsonSerializer.Deserialize<QaPolicyRules>(policy.RulesJson)
                ?? throw new InvalidDataException("The active QA policy deserialized to null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                $"QA policy '{policy.Id}' version '{policy.Version}' contains invalid RulesJson.",
                ex);
        }
    }

    public QaPolicyEvaluation Evaluate(
        QaRun run,
        IReadOnlyList<QaFinding> findings,
        IReadOnlyList<QaFindingState> states,
        IReadOnlyList<QaTestReliability> reliability,
        IReadOnlyList<QaWaiver> waivers,
        QaPolicyRules rules,
        QaPolicy? policy,
        int infrastructureFailures,
        long wallDurationMs,
        long cpuTimeMs,
        long artifactBytes,
        DateTimeOffset now)
    {
        var blockers = new List<string>();
        var warnings = new List<string>();
        var effective = findings.Where(f =>
            !waivers.Any(w => w.IsActive(now) && QaGovernanceMatching.MatchesWaiver(w, f))).ToArray();

        var critical = effective.Count(x => x.Severity == Severity.Critical);
        var warning = effective.Count(x => x.Severity == Severity.Warning);
        if (critical > rules.MaxCriticalFindings)
            blockers.Add($"{critical} non-waived critical finding(s) exceed policy limit {rules.MaxCriticalFindings}.");
        if (warning > rules.MaxWarningFindings)
            warnings.Add($"{warning} warning finding(s) exceed policy limit {rules.MaxWarningFindings}.");

        var flaky = reliability.Where(x => x.Classification == TestReliabilityClassification.Flaky).ToArray();
        if (flaky.Any(x => x.FailureRatePercent > rules.MaxFlakyRatePercent))
            warnings.Add("One or more flaky tests exceed the configured failure-rate limit.");
        if (run.Status == QaRunStatus.InfrastructureFailure && rules.BlockOnUnavailableQa)
            blockers.Add("QA infrastructure is unavailable; product quality cannot be verified.");
        if (infrastructureFailures > rules.MaxInfrastructureFailures)
            blockers.Add($"{infrastructureFailures} recent QA infrastructure failure(s) exceed policy limit {rules.MaxInfrastructureFailures}.");
        if (rules.MaxRunDurationMs > 0 && wallDurationMs > rules.MaxRunDurationMs)
            warnings.Add($"QA run duration {wallDurationMs} ms exceeds budget {rules.MaxRunDurationMs} ms.");

        var cpuSeconds = cpuTimeMs / 1000m;
        if (rules.MaxCpuSeconds > 0 && cpuSeconds > rules.MaxCpuSeconds)
            warnings.Add($"QA CPU time {cpuSeconds:0.###} s exceeds budget {rules.MaxCpuSeconds:0.###} s.");
        if (rules.MaxArtifactBytes > 0 && artifactBytes > rules.MaxArtifactBytes)
            warnings.Add($"QA artifacts {artifactBytes} bytes exceed budget {rules.MaxArtifactBytes} bytes.");

        var verdict = blockers.Count > 0
            ? QaGateVerdict.NoGo
            : warnings.Count > 0 ? QaGateVerdict.ReviewRequired : QaGateVerdict.Go;
        var summary = verdict switch
        {
            QaGateVerdict.Go => "GO: QA policy requirements are satisfied.",
            QaGateVerdict.ReviewRequired =>
                "REVIEW REQUIRED: no hard QA blocker is open, but quality warnings need explicit review.",
            _ => "NO-GO: QA policy contains one or more release blockers."
        };

        return new QaPolicyEvaluation(
            Guid.NewGuid(),
            run.ProjectId,
            run.Id,
            policy?.Id,
            policy?.Version ?? "default-v1",
            verdict,
            JsonSerializer.Serialize(blockers),
            JsonSerializer.Serialize(warnings),
            summary,
            now);
    }
}
