// Purpose: Defines the ProjectSourceBindingDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ProjectSourceBindingDto(Guid Id, Guid ProjectId, Guid ConnectionId, string ExternalResourceId, string ExternalResourceName, string ResourceType, bool Enabled, DateTimeOffset? LastSyncedAtUtc, DateTimeOffset UpdatedAtUtc);
