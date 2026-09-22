// Purpose: Defines the IProjectSourceAdapter application boundary.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public interface IProjectSourceAdapter { string ProviderName { get;
 } bool IsConfigured { get;
 } bool IsConfiguredFor(Guid projectId);
 Task<IReadOnlyList<ProjectSourceObservation>> PullAsync(Guid projectId, CancellationToken ct);
 }
