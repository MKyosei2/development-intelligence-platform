// Purpose: Defines A na ly si sR eq ue st ed repository behavior.
namespace Dip.Shared.Contracts.Events;

public sealed record AnalysisRequested(
    Guid ProjectId,
    Guid PipelineRunId,
    string AnalysisType,
    string InputReference);
