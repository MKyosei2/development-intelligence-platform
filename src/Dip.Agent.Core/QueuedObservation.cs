// Purpose: Defines the QueuedObservation application contract.
namespace Dip.Agent.Core;

public sealed record QueuedObservation(
    string Id,
    LocalFileObservation Observation,
    DateTimeOffset CreatedAtUtc,
    int Attempts);
