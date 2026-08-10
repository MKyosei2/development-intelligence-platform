using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.ProjectTruth;

public sealed class ProjectTruthEvaluatorTests
{
    [Fact]
    public void Evaluate_FailedRuntimeEvidence_MakesProjectAtRiskOrBlocked()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var claim = new ProjectClaim(
            Guid.NewGuid(), projectId, "project", "Complete", 100m,
            "developer", null, now);
        var evidence = new[]
        {
            Evidence(projectId, "integration", EvidenceOutcome.Succeeded, now),
            Evidence(projectId, "ci-check", EvidenceOutcome.Succeeded, now),
            Evidence(projectId, "runtime", EvidenceOutcome.Failed, now)
        };

        var result = CreateEvaluator().Evaluate([claim], evidence, now);

        result.TruthStatus.Should().Be(ProjectTruthStatus.Blocked);
        result.RiskLevel.Should().Be(ProjectRiskLevel.Critical);
        result.ContradictionCount.Should().Be(1);
        result.Blockers.Should().Contain(item => item.Severity == "Critical");
    }

    [Fact]
    public void Evaluate_MultipleStrongSourcesAndConfirmedClaim_CanBeHealthy()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();
        var claim = new ProjectClaim(
            Guid.NewGuid(), projectId, "project", "Complete", 100m,
            "developer", null, now);
        var evidence = new[]
        {
            Evidence(projectId, "integration", EvidenceOutcome.Succeeded, now, EvidenceSourceType.GitHub, EvidenceConfidence.Verified),
            Evidence(projectId, "build", EvidenceOutcome.Succeeded, now, EvidenceSourceType.Build, EvidenceConfidence.High),
            Evidence(projectId, "test", EvidenceOutcome.Succeeded, now, EvidenceSourceType.Test, EvidenceConfidence.High)
        };

        var result = CreateEvaluator().Evaluate([claim], evidence, now);

        result.Progress.ReportedProgress.Should().Be(100m);
        result.Progress.VerifiedProgress.Should().Be(100m);
        result.EvidenceConfidence.Should().BeOneOf(EvidenceConfidence.High, EvidenceConfidence.Verified);
        result.TruthStatus.Should().Be(ProjectTruthStatus.Healthy);
    }

    private static ProjectTruthEvaluator CreateEvaluator() => new(
        new ClaimVerificationEvaluator(),
        new EvidenceConfidenceEvaluator(),
        new ProgressEvaluator(),
        new ProjectBlockerEvaluator(),
        new ProjectRiskEvaluator());

    private static EvidenceRecord Evidence(
        Guid projectId,
        string kind,
        EvidenceOutcome outcome,
        DateTimeOffset at,
        EvidenceSourceType source = EvidenceSourceType.Build,
        EvidenceConfidence confidence = EvidenceConfidence.High) => new(
        Guid.NewGuid(), projectId, null, "project", source, kind, outcome,
        kind, $"{kind} result", confidence, null, null, at, at);
}
