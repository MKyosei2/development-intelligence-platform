// Purpose: Defines the SmartTestSelectionRequest application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record SmartTestSelectionRequest(Guid ProjectId,IReadOnlyList<string> ChangedPaths,int MaximumTests=100);
