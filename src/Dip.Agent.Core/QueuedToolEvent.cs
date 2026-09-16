// Purpose: Defines QueuedToolEvent.
using System.Text.Json;

namespace Dip.Agent.Core;

public sealed record QueuedToolEvent(
    string Id,
    ToolEventObservation Event,
    DateTimeOffset CreatedAtUtc,
    int Attempts);
