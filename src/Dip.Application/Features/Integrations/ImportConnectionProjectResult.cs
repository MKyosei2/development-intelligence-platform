// Purpose: Defines the ImportConnectionProjectResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ImportConnectionProjectResult(Guid ProjectId,string Name,string RepositoryUrl,string DefaultBranch,Guid BindingId,int EvidenceCreated,int EvidenceAlreadyPresent,string? Warning=null);
