// Purpose: Defines the LinkTaskToMilestoneCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record LinkTaskToMilestoneCommand(Guid ProjectId,string TaskKey,string MilestoneKey);
