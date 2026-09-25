// Purpose: Defines Outbox Message Tests responsibilities for this repository area.
using Dip.Domain.Entities;
using Xunit;

namespace Dip.Domain.Tests;

public sealed class OutboxMessageTests
{
    [Fact]
    public void MarkProcessed_SetsProcessedTimestamp()
    {
        var now = DateTimeOffset.UtcNow;
        var message = CreateMessage(now);

        message.MarkProcessed(now.AddSeconds(1));

        Assert.Equal(now.AddSeconds(1), message.ProcessedAtUtc);
        Assert.Null(message.LockedUntilUtc);
        Assert.Null(message.NextAttemptAtUtc);
        Assert.Null(message.LastError);
    }

    [Fact]
    public void LockThenMarkFailed_RecordsOneAttemptAndSchedulesRetry()
    {
        var now = DateTimeOffset.UtcNow;
        var message = CreateMessage(now);
        var nextAttemptAt = now.AddMinutes(1);

        message.Lock(now.AddSeconds(30));
        message.MarkFailed("error", nextAttemptAt);

        // AttemptCount increases when a delivery attempt is acquired by Lock.
        // MarkFailed records the outcome and must not count the same attempt twice.
        Assert.Equal(1, message.AttemptCount);
        Assert.Equal("error", message.LastError);
        Assert.Equal(nextAttemptAt, message.NextAttemptAtUtc);
        Assert.Null(message.LockedUntilUtc);
        Assert.Null(message.ProcessedAtUtc);
    }

    private static OutboxMessage CreateMessage(DateTimeOffset now) =>
        new(
            Guid.NewGuid(),
            "Test",
            "{}",
            "correlation-1",
            null,
            now);
}
