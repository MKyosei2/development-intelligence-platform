// Purpose: Verifies expected behavior and invariants for the surrounding feature.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.Exceptions;
using Dip.Domain.ValueObjects;
using Xunit;
namespace Dip.Domain.Tests;
public sealed class PipelineRunTests
{
    [Fact]
    public void Complete_Succeeds()
    {
        var now = DateTimeOffset.UtcNow; var r = Create(now); r.Start(now); r.Complete(now.AddMinutes(1));
        Assert.Equal(PipelineStatus.Succeeded, r.Status);
    }
    [Fact]
    public void Complete_RejectsReverseTime()
    {
        var now = DateTimeOffset.UtcNow; var r = Create(now); r.Start(now);
        Assert.Throws<DomainException>(() => r.Complete(now.AddSeconds(-1)));
    }
    [Fact]
    public void Timeout_Succeeds()
    {
        var now = DateTimeOffset.UtcNow; var r = Create(now); r.Start(now); r.Timeout("timeout", now.AddMinutes(5));
        Assert.Equal(PipelineStatus.TimedOut, r.Status);
    }
    private static PipelineRun Create(DateTimeOffset now) => new(Guid.NewGuid(), Guid.NewGuid(),
        new CommitSha("0123456789abcdef0123456789abcdef01234567"), "run-1", 1, "main", "generic-json", "1.0.0", now);
}
