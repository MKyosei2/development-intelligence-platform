// Purpose: Defines the ImportConnectionProjectCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ImportConnectionProjectCommand(string ExternalResourceId, string ExternalResourceName, string ProjectName, DateTimeOffset? ReleaseDateUtc = null, bool SyncEvidence = true);
