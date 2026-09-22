// Purpose: Exposes feature-readiness, review-intelligence, and asset-observation endpoints under the stable project-control route.
using Dip.Api.Authorization;
using Dip.Application.Abstractions.Integrations;
using Dip.Application.Features.Assets;
using Dip.Application.Features.ChangeImpact;
using Dip.Application.Features.Dashboard;
using Dip.Application.Features.Decisions;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.FeatureControl;
using Dip.Application.Features.Integrations;
using Dip.Application.Features.Management;
using Dip.Application.Features.Milestones;
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.Review;
using Dip.Application.Features.Scenarios;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectControlDecisionBriefDto = Dip.Application.Features.Decisions.DecisionBriefDto;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/project-control")]
public sealed class FeatureControlController(FeatureReadinessService features, ReviewIntelligenceService reviews, AssetIntelligenceService assets) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("features")]
    public Task<DevelopmentGraphNodeDto> UpsertFeature([FromBody] UpsertFeatureCommand command, CancellationToken ct)
            => features.UpsertFeatureAsync(command, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("acceptance-criteria")]
    public Task<DevelopmentGraphNodeDto> UpsertCriterion([FromBody] UpsertAcceptanceCriterionCommand command, CancellationToken ct)
            => features.UpsertCriterionAsync(command, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("acceptance-evidence")]
    public Task<DevelopmentGraphNodeDto> AttachAcceptanceEvidence([FromBody] AttachAcceptanceEvidenceCommand command, CancellationToken ct)
            => features.AttachEvidenceAsync(command, ct);

    [Authorize(Policy = Policies.ProjectRead)]
    [HttpGet("projects/{projectId:guid}/features/{featureKey}/readiness")]
    public Task<FeatureReadinessResult> GetFeatureReadiness(Guid projectId, string featureKey, [FromQuery] int evidenceFreshnessDays = 30, CancellationToken ct = default)
            => features.EvaluateAsync(projectId, featureKey, evidenceFreshnessDays, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("reviews/analyze")]
    public Task<ReviewIntelligenceResult> AnalyzeReview([FromBody] ReviewIntelligenceCommand command, CancellationToken ct)
            => reviews.AssessAsync(command, ct);

    [Authorize(Policy = Policies.ProjectWrite)]
    [HttpPost("assets/observe")]
    public Task<AssetIntelligenceResult> ObserveAsset([FromBody] AssetObservationCommand command, CancellationToken ct)
            => assets.ObserveAsync(command, ct);
}
