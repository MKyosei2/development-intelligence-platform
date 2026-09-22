// Purpose: Defines the CreateProjectSourceBindingCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record CreateProjectSourceBindingCommand(Guid ProjectId, Guid ConnectionId, string ExternalResourceId, string ExternalResourceName, string ResourceType, string? ConfigurationJson = null);
