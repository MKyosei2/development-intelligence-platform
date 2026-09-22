// Purpose: Defines the UpsertActionItemCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertActionItemCommand(Guid ProjectId, string Key, string Title, string Status = "Open", string Priority = "Medium", string? OwnerRole = null, DateTimeOffset? DueAtUtc = null, decimal CompletionPercent = 0m, string? DecisionKey = null, ManagementReference? Target = null, string? ExternalReference = null);
