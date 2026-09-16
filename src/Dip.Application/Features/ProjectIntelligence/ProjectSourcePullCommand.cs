// Purpose: Defines the ProjectSourcePullCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProjectSourcePullCommand(Guid ProjectId,string Provider,bool DryRun=false);
