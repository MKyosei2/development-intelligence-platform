// Purpose: Defines the EntityMatchDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record EntityMatchDto(string SourceNodeType,string SourceKey,string SourceName,string Provider,string? TargetNodeType,string? TargetKey,string? TargetName,decimal Confidence,string Resolution,string Reason,DateTimeOffset ObservedAtUtc);
