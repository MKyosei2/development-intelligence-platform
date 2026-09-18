// Purpose: Verifies behavior and invariants covered by E vi de nc eR ec or dT es ts.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Xunit;

namespace Dip.Domain.Tests;

public sealed class EvidenceRecordTests
{
    [Fact]
    public void Constructor_PreservesEvidenceProvenance()
    {
        var now = DateTimeOffset.UtcNow;
        var projectId = Guid.NewGuid();

        var evidence = new EvidenceRecord(
            Guid.NewGuid(),
            projectId,
            null,
            "project",
            EvidenceSourceType.GitHub,
            "ci-check",
            EvidenceOutcome.Succeeded,
            "CI passed",
            "The required check succeeded.",
            EvidenceConfidence.Verified,
            "https://example.test/run/1",
            null,
            now,
            now);

        Assert.Equal(projectId, evidence.ProjectId);
        Assert.Equal(EvidenceSourceType.GitHub, evidence.SourceType);
        Assert.Equal(EvidenceConfidence.Verified, evidence.Confidence);
        Assert.Equal(EvidenceOutcome.Succeeded, evidence.Outcome);
    }

    [Fact]
    public void Constructor_RejectsEmptyProjectId()
    {
        var now = DateTimeOffset.UtcNow;

        Assert.Throws<ArgumentException>(() => new EvidenceRecord(
            Guid.NewGuid(),
            Guid.Empty,
            null,
            "project",
            EvidenceSourceType.System,
            "test",
            EvidenceOutcome.Succeeded,
            "title",
            "summary",
            EvidenceConfidence.High,
            null,
            null,
            now,
            now));
    }
}
