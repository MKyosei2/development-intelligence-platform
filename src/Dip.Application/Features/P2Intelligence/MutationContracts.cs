// Purpose: Defines mutation-test inputs, per-case outcomes, and aggregate mutation-score results.
namespace Dip.Application.Features.P2Intelligence;

public sealed record MutationCaseSpec(
    string Id,
    string FileRelativePath,
    string OriginalText,
    string ReplacementText,
    string? Operator = null);

public sealed record MutationCommand(
    ExperimentTarget Target,
    IReadOnlyList<MutationCaseSpec> Mutations,
    int MaxMutations = 50,
    bool GenerateCommonMutations = false,
    IReadOnlyList<string>? AutoSourceFiles = null);

public sealed record MutationCaseResult(
    string Id,
    bool Applied,
    bool Killed,
    int? ExitCode,
    string Detail,
    string? FileRelativePath = null,
    int? Line = null,
    int? Column = null,
    string? Operator = null);

public sealed record MutationResult(
    string Status,
    int Total,
    int Applied,
    int Killed,
    int Survived,
    decimal MutationScorePercent,
    IReadOnlyList<MutationCaseResult> Cases,
    string ArtifactReference,
    bool BaselinePassed = false,
    string? BaselineDetail = null);
