// Purpose: Defines the IUnitOfWork application boundary.
using Dip.Domain.Entities;
namespace Dip.Application.Abstractions.Persistence;

public interface IUnitOfWork { Task<int> SaveChangesAsync(CancellationToken ct); }
