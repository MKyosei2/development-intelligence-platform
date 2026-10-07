// Purpose: Verifies Unreal QA availability remains fail-closed when Editor or AutomationTool runtime dependencies are unavailable.
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class UnrealProjectQaAdapterTests
{
    [Fact]
    public void AssessAvailability_WithoutEditor_IsNotRuntimeEvaluable()
    {
        var state = UnrealProjectQaAdapter.AssessAvailability(true, false, false);

        state.RuntimeQaEvaluable.Should().BeFalse();
        state.PackageQaEvaluable.Should().BeFalse();
        state.Status.Should().Be("NotEvaluated");
    }

    [Fact]
    public void AssessAvailability_WithEditorWithoutUat_IsPartialOnly()
    {
        var state = UnrealProjectQaAdapter.AssessAvailability(true, true, false);

        state.RuntimeQaEvaluable.Should().BeTrue();
        state.PackageQaEvaluable.Should().BeFalse();
        state.Status.Should().Be("PartiallyEvaluable");
    }

    [Fact]
    public void AssessAvailability_WithEditorAndUat_IsFullyEvaluable()
    {
        var state = UnrealProjectQaAdapter.AssessAvailability(true, true, true);

        state.RuntimeQaEvaluable.Should().BeTrue();
        state.PackageQaEvaluable.Should().BeTrue();
        state.Status.Should().Be("Evaluable");
    }
}
