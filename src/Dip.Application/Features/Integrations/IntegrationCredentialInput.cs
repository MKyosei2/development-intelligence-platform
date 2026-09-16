// Purpose: Defines the IntegrationCredentialInput application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record IntegrationCredentialInput(string? Token=null,long? AppId=null,long? InstallationId=null,string? AppSlug=null,string? PrivateKeyBase64=null);
