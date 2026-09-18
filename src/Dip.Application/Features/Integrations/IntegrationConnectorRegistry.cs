// Purpose: Defines the application boundary represented by I nt eg ra ti on Co nn ec to rR eg is tr y.
using Dip.Domain.Entities;

namespace Dip.Application.Features.Integrations;

public sealed class IntegrationConnectorRegistry
{
    private readonly IReadOnlyList<IIntegrationConnector> _connectors;

    public IntegrationConnectorRegistry(IEnumerable<IIntegrationConnector> connectors)
        => _connectors = connectors.ToArray();

    public IIntegrationConnector Resolve(IntegrationConnection connection)
        => _connectors.FirstOrDefault(connector =>
               connector.Provider.Equals(
                   connection.Provider,
                   StringComparison.OrdinalIgnoreCase)
               && connector.Supports(connection))
           ?? throw new InvalidOperationException(
               $"No integration connector supports provider '{connection.Provider}' " +
               $"and mode '{connection.Mode}'.");
}
