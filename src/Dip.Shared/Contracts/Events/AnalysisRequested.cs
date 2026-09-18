// Purpose: Defines the shared contract represented by A na ly si sR eq ue st ed.
namespace Dip.Shared.Contracts.Events;

public sealed record AnalysisRequested(
    Guid ProjectId,
    Guid PipelineRunId,
    string AnalysisType,
    string InputReference);
