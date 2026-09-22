// Purpose: Defines a focused responsibility within the development intelligence platform.
namespace Dip.Shared.Contracts.Events;

public sealed record AnalysisRequested(
    Guid ProjectId,
    Guid PipelineRunId,
    string AnalysisType,
    string InputReference);
