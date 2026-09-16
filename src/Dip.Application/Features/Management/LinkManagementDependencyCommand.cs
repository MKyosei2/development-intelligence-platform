// Purpose: Defines the LinkManagementDependencyCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record LinkManagementDependencyCommand(Guid ProjectId,string PredecessorType,string PredecessorKey,string SuccessorType,string SuccessorKey,bool Critical=true);
