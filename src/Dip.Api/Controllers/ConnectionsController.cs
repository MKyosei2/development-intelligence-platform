using Dip.Api.Authorization;
using Dip.Application.Features.Integrations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

public sealed record SetConnectionEnabledRequest(bool Enabled);

[ApiController]
[Route("api/v1/admin/connections")]
[Produces("application/json")]
public sealed class ConnectionsController(
    IntegrationConnectionService service,
    IConfiguration configuration) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<IntegrationConnectionDto>>> List(
        CancellationToken ct)
    {
        if (AdminDenied()) return Forbid();
        return Ok(await service.ListAsync(ct));
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<IntegrationConnectionDto>> Get(
        Guid id,
        CancellationToken ct)
    {
        if (AdminDenied()) return Forbid();
        var item = await service.GetAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost]
    public async Task<ActionResult<IntegrationConnectionDto>> Create(
        CreateIntegrationConnectionCommand command,
        CancellationToken ct)
    {
        if (AdminDenied()) return Forbid();
        var created = await service.CreateAsync(command, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<IntegrationConnectionDto>> Test(
        Guid id,
        CancellationToken ct)
    {
        if (AdminDenied()) return Forbid();
        try { return Ok(await service.TestAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpGet("{id:guid}/resources")]
    public async Task<ActionResult<IReadOnlyList<ConnectorResourceDto>>> Resources(
        Guid id,
        [FromQuery] string? search,
        [FromQuery] string? visibility,
        [FromQuery] int limit = 500,
        CancellationToken ct = default)
    {
        if (AdminDenied()) return Forbid();
        try
        {
            return Ok(await service.DiscoverAsync(
                id,
                new ConnectorDiscoveryQuery(search, visibility, limit),
                ct));
        }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpGet("{id:guid}/capabilities")]
    public async Task<ActionResult<IReadOnlyList<ConnectorCapability>>> Capabilities(
        Guid id,
        CancellationToken ct)
    {
        if (AdminDenied()) return Forbid();
        try { return Ok(await service.GetCapabilitiesAsync(id, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPatch("{id:guid}/enabled")]
    public async Task<ActionResult<IntegrationConnectionDto>> SetEnabled(
        Guid id,
        SetConnectionEnabledRequest request,
        CancellationToken ct)
    {
        if (AdminDenied()) return Forbid();
        try { return Ok(await service.SetEnabledAsync(id, request.Enabled, ct)); }
        catch (KeyNotFoundException) { return NotFound(); }
    }

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        if (AdminDenied()) return Forbid();
        try
        {
            await service.DeleteAsync(id, ct);
            return NoContent();
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private bool AdminDenied()
    {
        var secured =
            configuration.GetValue<bool?>("Authentication:RequireAuthenticatedUser") ?? false;
        return secured && !User.IsInRole(Roles.Admin);
    }
}
