// Purpose: Verifies expected behavior and invariants for the surrounding feature.
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
        var queuedAt = DateTimeOffset.UtcNow;
        var run = CreatePipelineRun(queuedAt);

        run.Start(queuedAt.AddSeconds(1));
        run.Complete(queuedAt.AddSeconds(10));

        run.Status.Should().Be(PipelineStatus.Succeeded);
        run.StartedAtUtc.Should().Be(queuedAt.AddSeconds(1));
        run.FinishedAtUtc.Should().Be(queuedAt.AddSeconds(10));
        run.ExternalRunId.Should().Be("test-run-1");
        run.AttemptNumber.Should().Be(1);
        run.Branch.Should().Be("main");
        run.CollectorType.Should().Be("generic-json");
        run.CollectorVersion.Should().Be("0.1.0");
    }

    [Fact]
    public void CompleteWithoutStart_Throws()
    {
        var queuedAt = DateTimeOffset.UtcNow;
        var run = CreatePipelineRun(queuedAt);

        var action = () =>
            run.Complete(queuedAt.AddSeconds(10));

        action.Should().Throw<DomainException>();
        run.Status.Should().Be(PipelineStatus.Queued);
        run.StartedAtUtc.Should().BeNull();
        run.FinishedAtUtc.Should().BeNull();
    }

    private static PipelineRun CreatePipelineRun(
        DateTimeOffset queuedAt)
    {
        return new PipelineRun(
            Guid.NewGuid(),
            Guid.NewGuid(),
            new CommitSha("abcdef1234567890"),
            "test-run-1",
            1,
            "main",
            "generic-json",
            "0.1.0",
            queuedAt);
    }
}
