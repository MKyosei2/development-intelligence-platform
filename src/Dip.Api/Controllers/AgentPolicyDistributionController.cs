// Purpose: Exposes Agent Policy Distribution HTTP endpoints for the DIP API.
using Dip.Api.Authorization;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Enterprise;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Dip.Api.Controllers;
[ApiController]
[Route("api/v1/enterprise/agents/{agentId}/policy")]
public sealed class AgentPolicyDistributionController(AgentPolicyDistributionService policies, IEnterpriseFleetRepository fleet) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet]
    public async Task<IActionResult> Get(string agentId, CancellationToken ct)
    {
        var agent = await fleet.FindAgentAsync(agentId, ct);
        if (agent is null) return NotFound();
        if (agent.Status == Dip.Domain.Enums.AgentFleetStatus.Revoked) return Forbid();
        return Ok(await policies.GetAsync(agent.ProjectId, agent.AgentId, agent.PolicyVersion + 1, ct));
    }
}
