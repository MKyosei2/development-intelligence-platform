// Purpose: Defines deterministic what-if simulation inputs.
namespace Dip.Application.Features.Scenarios;

public sealed record WhatIfSimulationCommand(
    Guid ProjectId,
    string ScenarioName,
    IReadOnlyList<string> ExcludedFeatureKeys,
    int ScheduleBufferDays = 0,
    decimal QaCapacityMultiplier = 1m);
