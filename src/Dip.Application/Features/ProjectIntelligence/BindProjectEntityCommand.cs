// Purpose: Defines the BindProjectEntityCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record BindProjectEntityCommand(Guid ProjectId, string SourceType, string SourceKey, string TargetType, string TargetKey, string Actor, string? Reason = null);
