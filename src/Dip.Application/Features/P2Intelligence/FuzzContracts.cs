// Purpose: Defines fuzz-execution configuration, case outcomes, minimized failure artifacts, and aggregate results.
namespace Dip.Application.Features.P2Intelligence;

public sealed record FuzzCommand(
    ExperimentTarget Target,
    string ArgumentPlaceholder = "{input}",
    int Cases = 100,
    int MaxInputBytes = 4096,
    int Seed = 1337,
    bool MinimizeFailures = true,
    int MaxMinimizationRunsPerFailure = 24);

public sealed record FuzzCaseResult(
    int Case,
    int Size,
    int? ExitCode,
    bool TimedOut,
    bool Crashed,
    string Fingerprint,
    string? InputReference = null);

public sealed record FuzzFailureArtifact(
    int Case,
    string Kind,
    string Fingerprint,
    string InputReference,
    string? MinimizedInputReference,
    int OriginalBytes,
    int? MinimizedBytes);

public sealed record FuzzResult(
    string Status,
    int Cases,
    int Crashes,
    int Timeouts,
    int UniqueFingerprints,
    IReadOnlyList<FuzzCaseResult> Results,
    string ArtifactReference,
    IReadOnlyList<FuzzFailureArtifact>? Failures = null);
