using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.Exceptions;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Domain.Tests.Entities;

public sealed class PipelineRunTests
{
    [Fact]
    public void StartThenComplete_TransitionsCorrectly()
    {
        var now = DateTimeOffset.UtcNow;
        var run = new PipelineRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CommitSha("abcdef1234567890"),
            "generic-json",
            "0.1.0",
            now);

        run.Start(now.AddSeconds(1));
        run.Complete(now.AddSeconds(10));

        run.Status.Should().Be(PipelineStatus.Succeeded);
        run.FinishedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public void CompleteWithoutStart_Throws()
    {
        var now = DateTimeOffset.UtcNow;
        var run = new PipelineRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CommitSha("abcdef1234567890"),
            "generic-json",
            "0.1.0",
            now);

        var action = () => run.Complete(now.AddSeconds(10));
        action.Should().Throw<DomainException>();
    }
}
