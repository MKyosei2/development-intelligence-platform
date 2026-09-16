// Purpose: Defines the IMetricRepository application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IMetricRepository{Task AddRangeAsync(IEnumerable<Metric> metrics,CancellationToken ct);}
