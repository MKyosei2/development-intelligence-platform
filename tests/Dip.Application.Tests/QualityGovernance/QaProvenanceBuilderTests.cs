// Purpose: Verifies provenance hashes are stable for equivalent evidence and change with the bound revision.
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaProvenanceBuilderTests
{
    [Fact]
    public void Build_SameRunAndEvidence_ProducesStableInputAndEvidenceHashes()
    {
        var run = NewRun("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var sut = new QaProvenanceBuilder();
        var now = DateTimeOffset.UtcNow;

        var first = sut.Build(run, [], "policy-v1", now);
        var second = sut.Build(run, [], "policy-v1", now);

        first.InputHash.Should().Be(second.InputHash);
        first.EvidenceHash.Should().Be(second.EvidenceHash);
    }

    [Fact]
    public void Build_DifferentCommit_ChangesInputHash()
    {
        var sut = new QaProvenanceBuilder();
        var now = DateTimeOffset.UtcNow;

        var first = sut.Build(NewRun("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"), [], "policy-v1", now);
        var second = sut.Build(NewRun("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"), [], "policy-v1", now);

        first.InputHash.Should().NotBe(second.InputHash);
    }

    private static QaRun NewRun(string sha) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "unit-test", new LanguageId("csharp"),
            ".", sha, ".", new DateTimeOffset(2026, 9, 22, 0, 0, 0, TimeSpan.Zero));
}
