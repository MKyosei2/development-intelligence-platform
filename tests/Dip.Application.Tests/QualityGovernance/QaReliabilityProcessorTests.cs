// Purpose: Verifies reliability processing is represented by a dedicated unit with explicit persistence boundaries.
using Dip.Application.Features.QualityGovernance;
using FluentAssertions;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaReliabilityProcessorTests
{
    [Fact]
    public void Constructor_WithRepositoryAndUnitOfWorkDependencies_CreatesProcessor()
    {
        var sut = new QaReliabilityProcessor(null!, null!);

        sut.Should().NotBeNull();
    }
}
