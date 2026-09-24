// Purpose: Verifies P ro ce ss ed We bh oo k behavior, edge cases, and invariants for this repository area.
using Dip.Domain.Entities;
using Xunit;

namespace Dip.Domain.Tests;

public sealed class ProcessedWebhookTests
{
    [Fact]
    public void MarkProcessed_SetsProcessedAt()
    {
        var webhook = new ProcessedWebhook(
            "delivery-1",
            "pull_request",
            new string('A', 64),
            DateTimeOffset.UtcNow);

        webhook.MarkProcessed(DateTimeOffset.UtcNow);

        Assert.NotNull(webhook.ProcessedAtUtc);
    }

    [Fact]
    public void MarkFailed_IncrementsAttemptCount()
    {
        var webhook = new ProcessedWebhook(
            "delivery-1",
            "pull_request",
            new string('A', 64),
            DateTimeOffset.UtcNow);

        webhook.MarkFailed("failed");

        Assert.Equal(1, webhook.AttemptCount);
    }
}
