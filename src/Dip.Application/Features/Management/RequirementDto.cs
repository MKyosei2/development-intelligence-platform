// Purpose: Defines the RequirementDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record RequirementDto(string Key,string Title,string Priority,string Status,string ScopeState,decimal CompletionPercent,string? AcceptanceSummary,string? ExternalReference);
