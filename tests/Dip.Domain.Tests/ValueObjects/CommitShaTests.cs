// Purpose: Verifies C om mi tS ha behavior, edge cases, and invariants for this repository area.
using Dip.Domain.Exceptions;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Domain.Tests.ValueObjects;

public sealed class CommitShaTests
{
    [Theory]
    [InlineData("abcdef1")]
    [InlineData("ABCDEF1234567890")]
    public void Constructor_AcceptsValidHex(string value)
    {
        var sha = new CommitSha(value);
        sha.Value.Should().Be(value.ToLowerInvariant());
    }

    [Theory]
    [InlineData("")]
    [InlineData("123")]
    [InlineData("not-a-sha")]
    public void Constructor_RejectsInvalidValue(string value)
    {
        var action = () => new CommitSha(value);
        action.Should().Throw<DomainException>();
    }
}
