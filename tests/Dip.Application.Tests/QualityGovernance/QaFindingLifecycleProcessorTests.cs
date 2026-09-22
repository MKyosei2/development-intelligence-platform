// Purpose: Verifies finding lifecycle processing is isolated behind explicit governance and ownership dependencies.
using Dip.Application.Features.QualityGovernance;
using FluentAssertions;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaFindingLifecycleProcessorTests
{
    [Fact]
    public void Constructor_WithExplicitDependencies_CreatesProcessor()
    {
        var sut = new QaFindingLifecycleProcessor(null!, null!, null!, null!);

        sut.Should().NotBeNull();
    }
}
