// Purpose: Defines the ProjectSourceProviderDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProjectSourceProviderDto(string Provider,bool Configured);
