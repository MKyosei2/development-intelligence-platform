// Purpose: Defines the UpsertDevelopmentGraphNodeCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record UpsertDevelopmentGraphNodeCommand(Guid ProjectId, DevelopmentGraphNodeType Type, string Key, string Name, string Status = "Observed", string? Provider = null, string? ExternalReference = null, string MetadataJson = "{}");
