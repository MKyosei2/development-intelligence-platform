// Purpose: Defines the IntegrationConnectionDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record IntegrationConnectionDto(Guid Id, string Provider, string DisplayName, IntegrationConnectionMode Mode, string? BaseUrl, bool Enabled, string? ExternalAccount, ConnectionHealthState HealthState, string? LastHealthMessage, DateTimeOffset? LastTestedAtUtc, DateTimeOffset UpdatedAtUtc);
