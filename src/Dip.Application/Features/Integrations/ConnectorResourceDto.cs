// Purpose: Defines the ConnectorResourceDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ConnectorResourceDto(string ExternalId, string ResourceType, string Name, string FullName, string? Url, string? DefaultBranch, bool Private, bool Archived, string? PrimaryLanguage, string? Description, DateTimeOffset? UpdatedAtUtc, Guid? DipProjectId = null);
