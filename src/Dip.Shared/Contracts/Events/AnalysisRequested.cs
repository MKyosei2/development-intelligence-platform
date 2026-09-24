// Purpose: Implements Analysis Requested responsibilities for events.
namespace Dip.Shared.Contracts.Events;

public sealed record AnalysisRequested(
    Guid ProjectId,
    Guid PipelineRunId,
    string AnalysisType,
    string InputReference);
