// Purpose: Defines the UpsertRequirementCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record UpsertRequirementCommand(Guid ProjectId, string Key, string Title, string Priority, string Status, string ScopeState = "Included", decimal CompletionPercent = 0m, string? AcceptanceSummary = null, string? ExternalReference = null);
