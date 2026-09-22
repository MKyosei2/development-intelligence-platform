// Purpose: Defines the PolicyProfileDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Policies;

public sealed record PolicyProfileDto(Guid Id, PolicyScopeType ScopeType, Guid? ProjectId, string? AgentId, string Name, int Version, bool Enabled, PolicyProfileDocument Document, DateTimeOffset UpdatedAtUtc);
