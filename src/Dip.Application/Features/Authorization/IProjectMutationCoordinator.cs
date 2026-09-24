// Purpose: Implements I Project Mutation Coordinator responsibilities for authorization.
namespace Dip.Application.Features.Authorization;

/// <summary>Serializes cross-row project governance mutations that cannot be protected by a single row version token.</summary>
public interface IProjectMutationCoordinator
{
    Task<T> ExecuteAsync<T>(Guid projectId, Func<CancellationToken, Task<T>> action, CancellationToken ct);
    Task ExecuteAsync(Guid projectId, Func<CancellationToken, Task> action, CancellationToken ct);
}
