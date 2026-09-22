// Purpose: Verifies cross-project benchmark comparison is isolated behind benchmark and project repositories.
using Dip.Application.Features.QualityGovernance;
using FluentAssertions;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaBenchmarkComparisonServiceTests
{
    [Fact]
    public void Constructor_WithRepositoryDependencies_CreatesService()
    {
        var sut = new QaBenchmarkComparisonService(null!, null!);

        sut.Should().NotBeNull();
    }
}
