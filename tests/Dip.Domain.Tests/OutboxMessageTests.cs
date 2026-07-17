using Dip.Domain.Entities;
using Xunit;

namespace Dip.Domain.Tests;

public sealed class OutboxMessageTests
{
    [Fact]
    public void MarkProcessed_SetsProcessedTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new OutboxMessage(
            Guid.NewGuid(),
            "Test",
            "{}",
            "correlation-1",
            null,
            now);

        message.MarkProcessed(now.AddSeconds(1));

        Assert.NotNull(message.ProcessedAtUtc);
    }

    [Fact]
    public void MarkFailed_IncrementsAttempt()
    {
        var now = DateTimeOffset.UtcNow;
        var message = new OutboxMessage(
            Guid.NewGuid(),
            "Test",
            "{}",
            "correlation-1",
            null,
            now);

        message.MarkFailed("error", now.AddMinutes(1));

        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("error", message.LastError);
    }
}
