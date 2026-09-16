// Purpose: Defines the LocalFileBatchRequest application contract.
namespace Dip.Agent.Core;

public sealed record LocalFileBatchRequest(
    string AgentId,
    string RootId,
    IReadOnlyList<LocalFileObservation> Observations);
