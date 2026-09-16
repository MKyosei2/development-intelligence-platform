// Purpose: Defines the ProductionToolBatchResult application contract.
using System.Text.Json;
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProductionTools;

public sealed record ProductionToolBatchResult(int Received,int Accepted,int Duplicates,int Rejected,IReadOnlyList<ProductionToolEventResult> Results,IReadOnlyList<string> Warnings);
