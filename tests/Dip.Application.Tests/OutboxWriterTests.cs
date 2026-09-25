// Purpose: Verifies Outbox Writer behavior, edge cases, and repository invariants.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Services;
using Dip.Domain.Entities;
using Xunit;

namespace Dip.Application.Tests;

public sealed class OutboxWriterTests
{
    [Fact]
    public async Task EnqueueAsync_CreatesOutboxMessage()
    {
        var repository = new FakeOutboxRepository();
        var writer = new OutboxWriter(repository);

        await writer.EnqueueAsync(
            new { Value = 1 },
            "correlation-1",
            null,
            DateTimeOffset.UtcNow,
            CancellationToken.None);

        Assert.Single(repository.Messages);
    }

    private sealed class FakeOutboxRepository : IOutboxRepository
    {
        public List<OutboxMessage> Messages { get; } = new();

        public Task AddAsync(
            OutboxMessage message,
            CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<OutboxMessage>> GetPendingAsync(
            int batchSize,
            DateTimeOffset now,
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<OutboxMessage>>(Messages);

        public Task<OutboxMessage?> GetByIdAsync(
            Guid id,
            CancellationToken cancellationToken) =>
            Task.FromResult(Messages.SingleOrDefault(x => x.Id == id));
    }
}
