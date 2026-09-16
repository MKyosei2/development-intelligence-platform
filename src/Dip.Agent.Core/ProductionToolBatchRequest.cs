// Purpose: Defines the ProductionToolBatchRequest application contract.
using System.Text.Json;

namespace Dip.Agent.Core;

public sealed record ProductionToolBatchRequest(
    string AgentId,
    IReadOnlyList<ToolEventObservation> Events);
