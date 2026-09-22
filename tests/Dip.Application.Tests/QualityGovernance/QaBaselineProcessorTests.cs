// Purpose: Verifies regression baseline processing ignores runs that cannot be bound to an exact commit.
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.ValueObjects;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaBaselineProcessorTests
{
    [Fact]
    public async Task ObserveAsync_WithoutCommitSha_ReturnsWithoutRepositoryAccess()
    {
        var run = new QaRun(Guid.NewGuid(), Guid.NewGuid(), "unit-test",
            new LanguageId("csharp"), ".", null, ".", DateTimeOffset.UtcNow);
        var sut = new QaBaselineProcessor(null!);

        await sut.ObserveAsync(run, [], DateTimeOffset.UtcNow, CancellationToken.None);
    }
}
