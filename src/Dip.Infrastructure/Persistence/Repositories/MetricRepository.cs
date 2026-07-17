using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class MetricRepository : IMetricRepository
{
    private readonly DipDbContext _db;

    public MetricRepository(DipDbContext db) => _db = db;

    public Task AddRangeAsync(
        IEnumerable<Metric> metrics,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        _db.Metrics.AddRange(metrics);
        return Task.CompletedTask;
    }
}
