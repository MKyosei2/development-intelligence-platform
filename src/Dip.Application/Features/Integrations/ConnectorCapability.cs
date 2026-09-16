// Purpose: Defines ConnectorCapability.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ConnectorCapability(string Key,bool Supported,string? Reason=null);
