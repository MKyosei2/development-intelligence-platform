// Purpose: Defines the IRepositoryOwnershipResolver application boundary.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public interface IRepositoryOwnershipResolver { Task<RepositoryOwnershipResolution?> ResolveAsync(string workspaceRelativePath, string? filePath, CancellationToken ct); }
