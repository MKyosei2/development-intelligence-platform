// Purpose: Defines deterministic what-if simulation outputs.
namespace Dip.Application.Features.Scenarios;

public sealed record WhatIfSimulationResult(
    string ScenarioName,
    decimal BaselineRisk,
    decimal SimulatedRisk,
    int BaselineOpenFeatures,
    int SimulatedOpenFeatures,
    int EstimatedScheduleDeltaDays,
    decimal EstimatedQaLoadChangePercent,
    IReadOnlyList<string> ChangedAssumptions,
    IReadOnlyList<string> Tradeoffs,
    string Summary);
