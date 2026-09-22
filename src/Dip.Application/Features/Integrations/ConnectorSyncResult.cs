// Purpose: Defines the ConnectorSyncResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ConnectorSyncResult(int Observed, IReadOnlyList<ConnectorSyncObservation> Observations, IReadOnlyList<string> Warnings);
