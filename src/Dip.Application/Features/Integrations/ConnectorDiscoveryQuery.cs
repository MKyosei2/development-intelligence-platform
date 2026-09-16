// Purpose: Defines the ConnectorDiscoveryQuery application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ConnectorDiscoveryQuery(string? Search=null,string? Visibility=null,int Limit=500);
