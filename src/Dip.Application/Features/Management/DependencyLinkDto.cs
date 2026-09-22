// Purpose: Defines the DependencyLinkDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record DependencyLinkDto(string SourceType, string SourceKey, string SourceName, string TargetType, string TargetKey, string TargetName, string Relation, bool Critical, bool CrossWorkstream);
