// Purpose: Defines the LinkRequirementCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record LinkRequirementCommand(Guid ProjectId, string RequirementKey, string TargetType, string TargetKey);
