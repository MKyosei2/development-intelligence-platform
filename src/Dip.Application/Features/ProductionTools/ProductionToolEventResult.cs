// Purpose: Defines the ProductionToolEventResult application contract.
using System.Text.Json;
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProductionTools;

public sealed record ProductionToolEventResult(string EventId,Guid EvidenceId,bool Created,EvidenceEventType EvidenceEventType);
