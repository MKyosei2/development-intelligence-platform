// Purpose: Defines the CreateWaiverCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record CreateWaiverCommand(Guid ProjectId,string Name,string Reason,string? Fingerprint,string? RuleId,string? PathPattern,DateTimeOffset ExpiresAtUtc);
