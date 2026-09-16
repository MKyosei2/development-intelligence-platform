// Purpose: Defines the CreateIntegrationConnectionCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record CreateIntegrationConnectionCommand(string Provider,string DisplayName,IntegrationConnectionMode Mode,string? BaseUrl,IntegrationCredentialInput Credential,bool TestAfterSave=true);
