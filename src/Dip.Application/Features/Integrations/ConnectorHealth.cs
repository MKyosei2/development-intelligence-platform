// Purpose: Defines ConnectorHealth.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ConnectorHealth(ConnectionHealthState State,string Message,DateTimeOffset CheckedAtUtc,string? ExternalAccount,IReadOnlyList<ConnectorCapability> Capabilities);
