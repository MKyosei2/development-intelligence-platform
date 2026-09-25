// Purpose: Persists and queries Metric state for DIP.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
namespace Dip.Infrastructure.Persistence.Repositories;
public sealed class MetricRepository(DipDbContext db) : IMetricRepository { public Task AddRangeAsync(IEnumerable<Metric> metrics, CancellationToken ct) { db.Metrics.AddRange(metrics); return Task.CompletedTask; } }
