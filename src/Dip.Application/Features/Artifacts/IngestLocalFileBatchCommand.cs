// Purpose: Defines the IngestLocalFileBatchCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Artifacts;

public sealed record IngestLocalFileBatchCommand(Guid ProjectId, string AgentId, string RootId, IReadOnlyList<LocalFileObservation> Observations);
