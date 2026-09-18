// Purpose: Verifies revision applicability and semantic claim handling for the three-pillar portfolio path.
using System.Text.Json;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectTruth;

public sealed class ThreePillarPrecisionTests
{
    private readonly ClaimVerificationEvaluator _evaluator = new();

    [Fact]
    public void CompleteClaim_DoesNotCombineHistoricalAndCurrentRevisionSuccesses()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var claim = Claim(projectId, now, "Complete");

        var evidence = new[]
        {
            Evidence(projectId, "build", EvidenceOutcome.Succeeded, now.AddMinutes(-10), "aaaaaaaa", Guid.NewGuid()),
            Evidence(projectId, "test", EvidenceOutcome.Succeeded, now, "bbbbbbbb", Guid.NewGuid())
        };

        var result = _evaluator.Evaluate(claim, evidence, now);

        result.Status.Should().Be(ClaimVerificationStatus.PartiallyConfirmed);
        result.EvidenceLinks.Should().Contain(link =>
            link.Relation == EvidenceRelation.Neutral
            && link.Reason.Contains("historical revision", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DerivedEvidence_InSamePipelineRun_InheritsRevisionAndCanConfirmCompletion()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var runId = Guid.NewGuid();
        var claim = Claim(projectId, now, "Complete");

        var evidence = new[]
        {
            Evidence(projectId, "pipeline", EvidenceOutcome.Succeeded, now, "cccccccc", runId, EvidenceSourceType.Unity),
            Evidence(projectId, "quality-gate", EvidenceOutcome.Succeeded, now, null, runId, EvidenceSourceType.Quality)
        };

        var result = _evaluator.Evaluate(claim, evidence, now);

        result.Status.Should().Be(ClaimVerificationStatus.Confirmed);
        result.EvidenceLinks.Count(link => link.Relation == EvidenceRelation.Supports).Should().Be(2);
    }

    [Fact]
    public void BlockedClaim_FailedRuntimeEvidenceSupportsInsteadOfContradicting()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var claim = Claim(projectId, now, "Blocked");
        var evidence = new[]
        {
            Evidence(projectId, "runtime", EvidenceOutcome.Failed, now, "dddddddd", Guid.NewGuid(), EvidenceSourceType.Runtime)
        };

        var result = _evaluator.Evaluate(claim, evidence, now);

        result.Status.Should().Be(ClaimVerificationStatus.Confirmed);
        result.EvidenceLinks.Should().ContainSingle(link => link.Relation == EvidenceRelation.Supports);
    }

    [Fact]
    public void JapaneseNotComplete_IsNotMisclassifiedAsComplete()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var claim = Claim(projectId, now, "未完了");
        var evidence = new[]
        {
            Evidence(projectId, "build", EvidenceOutcome.Succeeded, now, "eeeeeeee", Guid.NewGuid())
        };

        var result = _evaluator.Evaluate(claim, evidence, now);

        // Non-completion reports only need one strong supporting observation;
        // a completion report would require two independent evidence kinds.
        result.Status.Should().Be(ClaimVerificationStatus.Confirmed);
    }

    [Fact]
    public void CurrentRevisionFailure_ContradictsHistoricalPasses()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var claim = Claim(projectId, now, "Complete");
        var evidence = new[]
        {
            Evidence(projectId, "build", EvidenceOutcome.Succeeded, now.AddMinutes(-20), "11111111", Guid.NewGuid()),
            Evidence(projectId, "test", EvidenceOutcome.Succeeded, now.AddMinutes(-19), "11111111", Guid.NewGuid()),
            Evidence(projectId, "runtime", EvidenceOutcome.Failed, now, "22222222", Guid.NewGuid(), EvidenceSourceType.Runtime)
        };

        var result = _evaluator.Evaluate(claim, evidence, now);

        result.Status.Should().Be(ClaimVerificationStatus.Contradicted);
    }

    [Fact]
    public void EvidenceConfidence_DoesNotPromoteHistoricalSourcesIntoCurrentVerifiedState()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var evaluator = new EvidenceConfidenceEvaluator();
        var evidence = new[]
        {
            Evidence(projectId, "build", EvidenceOutcome.Succeeded, now.AddMinutes(-20), "33333333", Guid.NewGuid(), EvidenceSourceType.Build, EvidenceConfidence.Verified),
            Evidence(projectId, "test", EvidenceOutcome.Succeeded, now.AddMinutes(-19), "33333333", Guid.NewGuid(), EvidenceSourceType.Test, EvidenceConfidence.Verified),
            Evidence(projectId, "runtime", EvidenceOutcome.Succeeded, now, "44444444", Guid.NewGuid(), EvidenceSourceType.Runtime, EvidenceConfidence.Verified)
        };

        evaluator.Evaluate(evidence, now).Should().Be(EvidenceConfidence.High);
    }

    [Fact]
    public void ProjectTruth_DoesNotKeepHistoricalFailureAsCurrentBlocker()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var claim = Claim(projectId, now, "Complete");
        var evidence = new[]
        {
            Evidence(projectId, "runtime", EvidenceOutcome.Failed, now.AddMinutes(-20), "55555555", Guid.NewGuid(), EvidenceSourceType.Runtime),
            Evidence(projectId, "build", EvidenceOutcome.Succeeded, now.AddMinutes(-1), "66666666", Guid.NewGuid(), EvidenceSourceType.Build, EvidenceConfidence.Verified),
            Evidence(projectId, "test", EvidenceOutcome.Succeeded, now, "66666666", Guid.NewGuid(), EvidenceSourceType.Test, EvidenceConfidence.Verified)
        };

        var evaluator = new ProjectTruthEvaluator(
            new ClaimVerificationEvaluator(),
            new EvidenceConfidenceEvaluator(),
            new ProgressEvaluator(),
            new ProjectBlockerEvaluator(),
            new ProjectRiskEvaluator());

        var result = evaluator.Evaluate([claim], evidence, now);

        result.ContradictionCount.Should().Be(0);
        result.RiskLevel.Should().NotBe(ProjectRiskLevel.Critical);
        result.Blockers.Should().NotContain(item => item.Title.Contains("runtime", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void AdaptiveRisk_IgnoresFailuresFromHistoricalRevision()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var evidence = new List<EvidenceRecord>();
        for (var i = 0; i < 6; i++)
        {
            evidence.Add(Evidence(
                projectId,
                $"runtime-{i}",
                EvidenceOutcome.Failed,
                now.AddMinutes(-20 - i),
                "77777777",
                Guid.NewGuid(),
                EvidenceSourceType.Runtime));
        }
        evidence.Add(Evidence(
            projectId,
            "runtime-current",
            EvidenceOutcome.Succeeded,
            now,
            "88888888",
            Guid.NewGuid(),
            EvidenceSourceType.Runtime));

        var result = new AdaptiveProjectRiskEvaluator().Evaluate(
            ProjectRiskLevel.Low,
            evidence,
            Array.Empty<ProjectTruthSnapshot>(),
            now);

        result.RiskLevel.Should().Be(ProjectRiskLevel.Low);
        result.RecentFailureRatePercent.Should().Be(0m);
    }

    private static ProjectClaim Claim(Guid projectId, DateTimeOffset at, string status) =>
        new(Guid.NewGuid(), projectId, "project", status, 100m, "reporter", null, at);

    private static EvidenceRecord Evidence(
        Guid projectId,
        string kind,
        EvidenceOutcome outcome,
        DateTimeOffset at,
        string? commit,
        Guid? runId,
        EvidenceSourceType source = EvidenceSourceType.Build,
        EvidenceConfidence confidence = EvidenceConfidence.High)
    {
        var payload = commit is null
            ? null
            : JsonSerializer.Serialize(new { commitSha = commit, provider = source.ToString() });

        return new EvidenceRecord(
            Guid.NewGuid(),
            projectId,
            runId,
            "project",
            source,
            kind,
            outcome,
            kind,
            $"{kind} result",
            confidence,
            null,
            payload,
            at,
            at);
    }
}
