// Purpose: Defines the CreateScopeBaselineCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record CreateScopeBaselineCommand(Guid ProjectId,string Key,string Name,DateTimeOffset? CapturedAtUtc=null);
