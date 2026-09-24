// Purpose: Exposes C ap ab il it ie s HTTP endpoints for c on tr ol le rs.
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
