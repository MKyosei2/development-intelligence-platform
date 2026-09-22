// Purpose: Defines the ProductionToolBatchRequest application contract.
using System.Text.Json;
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProductionTools;

public sealed record ProductionToolBatchRequest(string AgentId, IReadOnlyList<ProductionToolObservation> Events);
