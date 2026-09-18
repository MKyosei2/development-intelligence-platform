// Purpose: Verifies behavior and invariants covered by H ea lt hS co re Te st s.
using Dip.Domain.Exceptions;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Domain.Tests.ValueObjects;

public sealed class HealthScoreTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(50)]
    [InlineData(100)]
    public void Constructor_AcceptsRange(decimal value)
    {
        new HealthScore(value).Value.Should().Be(value);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Constructor_RejectsOutOfRange(decimal value)
    {
        var action = () => new HealthScore(value);
        action.Should().Throw<DomainException>();
    }
}
