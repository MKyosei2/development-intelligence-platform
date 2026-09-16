// Purpose: Exposes C ap ab il it ie sC on tr ol le r HTTP endpoints.
using Dip.Application.Features.Capabilities;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/capabilities")]
public sealed class CapabilitiesController : ControllerBase
{
    [HttpGet]
    public IReadOnlyList<CapabilityDescriptor> List()
        => CapabilityRegistry.All;
}
