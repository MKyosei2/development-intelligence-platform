// Purpose: Defines the RepositoryOwnershipResolution application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record RepositoryOwnershipResolution(string Team, string? Workstream, string Pattern, string Source);
