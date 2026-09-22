// Purpose: Defines a focused responsibility within the development intelligence platform.
using Dip.Domain.Entities;

namespace Dip.Application.Features.Integrations;

public interface IIntegrationConnector
{
    string Provider { get; }

    bool Supports(IntegrationConnection connection);

    Task<ConnectorHealth> TestConnectionAsync(
        IntegrationConnection connection,
        CancellationToken ct);

    Task<IReadOnlyList<ConnectorResourceDto>> DiscoverAsync(
        IntegrationConnection connection,
        ConnectorDiscoveryQuery query,
        CancellationToken ct);

    Task<ConnectorSyncResult> SyncAsync(
        IntegrationConnection connection,
        ProjectSourceBinding binding,
        CancellationToken ct);

    Task<IReadOnlyList<ConnectorCapability>> GetCapabilitiesAsync(
        IntegrationConnection connection,
        CancellationToken ct);
}
