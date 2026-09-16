// Purpose: Defines the UniversalGraphNodeDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.DevelopmentGraph;

public sealed record UniversalGraphNodeDto(DevelopmentGraphNodeDto Node,string DomainId,string Kind,string SchemaVersion,string? SourceObjectId);
