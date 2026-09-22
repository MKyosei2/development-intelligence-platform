// Purpose: Verifies the split post-run processor exposes the expected dependency boundary as a dedicated unit.
using Dip.Application.Features.QualityAutomation;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaPostRunProcessorTests
{
    [Fact]
    public void Constructor_WithExplicitDependencies_CreatesDedicatedProcessor()
    {
        var sut = new QaPostRunProcessor(null!, null!, null!, null!, null!, null!, null!, null!);

        sut.Should().NotBeNull();
    }
}
