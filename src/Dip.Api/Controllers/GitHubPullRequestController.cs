// Purpose: Exposes project pull-request summaries and evidence-enriched pull-request details.
using System.Text.Json;
using Dip.Api.Authorization;
using Dip.Api.Hubs;
using Dip.Application.Abstractions.GitHub;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.Authorization;
using Dip.Application.Features.DevelopmentGraph;
using Dip.Application.Features.GitHubIntegration;
using Dip.Application.Features.Projects;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dip.Api.Controllers;

[ApiController]
[Route("api/v1/integrations/github")]
[Produces("application/json")]
public sealed class GitHubPullRequestController(ProjectService projects, DevelopmentGraphService developmentGraph, IPipelineRunRepository pipelineRuns, IQualityIssueRepository qualityIssues, IEvidenceRepository evidence) : ControllerBase
{
    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/pull-requests")]
    public async Task<ActionResult<IReadOnlyList<PullRequestSummaryResponse>>> PullRequests(
            Guid projectId,
            CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
            return NotFound();

        var graph = await developmentGraph.GetAsync(projectId, null, cancellationToken);
        var summaries = graph.Nodes
            .Where(node => node.Type.Equals(
                DevelopmentGraphNodeType.PullRequest.ToString(),
                StringComparison.OrdinalIgnoreCase))
            .Select(node => BuildPullRequestDetails(graph, node))
            .Where(details => details is not null)
            .Select(details => details!.Summary)
            .OrderByDescending(x => x.Number)
            .ToArray();
        return Ok(summaries);
    }

    [Authorize(Policy = Policies.ProjectRead)]
    [RequireProjectRole(ProjectMembershipRole.Viewer, "projectId")]
    [HttpGet("projects/{projectId:guid}/pull-requests/{number:int}")]
    public async Task<ActionResult<PullRequestDetailsResponse>> PullRequest(
            Guid projectId,
            int number,
            CancellationToken cancellationToken)
    {
        var project = await projects.GetAsync(projectId, cancellationToken);
        if (project is null)
            return NotFound();

        var graph = await developmentGraph.GetAsync(projectId, null, cancellationToken);
        var node = graph.Nodes.FirstOrDefault(candidate =>
            candidate.Type.Equals(DevelopmentGraphNodeType.PullRequest.ToString(), StringComparison.OrdinalIgnoreCase)
            && PullRequestNumber(candidate) == number);
        if (node is null)
            return NotFound();

        var details = BuildPullRequestDetails(graph, node);
        if (details is null)
            return NotFound();
        return Ok(await EnrichPullRequestDetailsAsync(projectId, details, cancellationToken));
    }

    private static PullRequestDetailsResponse? BuildPullRequestDetails(
            DevelopmentGraphDto graph,
            DevelopmentGraphNodeDto pullRequest)
    {
        var number = PullRequestNumber(pullRequest);
        if (!number.HasValue)
            return null;

        var byId = graph.Nodes.ToDictionary(node => node.Id);
        var commitIds = graph.Edges
            .Where(edge =>
                edge.SourceNodeId == pullRequest.Id
                && edge.Type.Equals(DevelopmentGraphEdgeType.Contains.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.TargetNodeId)
            .Where(id => byId.TryGetValue(id, out var node)
                         && node.Type.Equals(DevelopmentGraphNodeType.Commit.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToHashSet();

        var revisionNodes = commitIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .ToArray();
        var revisions = revisionNodes
            .Select(node => MetaString(node.MetadataJson, "Revision") ?? StripKeyTail(node.Key))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var fileIds = graph.Edges
            .Where(edge =>
                commitIds.Contains(edge.SourceNodeId)
                && edge.Type.Equals(DevelopmentGraphEdgeType.Changes.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.TargetNodeId)
            .ToHashSet();
        var changedFiles = fileIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Where(node => node.Type.Equals(DevelopmentGraphNodeType.File.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(node => node.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var reviewEdges = graph.Edges
            .Where(edge =>
                edge.TargetNodeId == pullRequest.Id
                && edge.Type.Equals(DevelopmentGraphEdgeType.Reviews.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var reviewNodes = reviewEdges
            .Select(edge => edge.SourceNodeId)
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Where(node => node.Type.Equals(DevelopmentGraphNodeType.Review.ToString(), StringComparison.OrdinalIgnoreCase))
            .ToArray();
        var assessment = reviewNodes.FirstOrDefault(node =>
            node.Key.StartsWith("review-assessment:", StringComparison.OrdinalIgnoreCase));
        var reviews = reviewNodes
            .Where(node => !node.Key.StartsWith("review-assessment:", StringComparison.OrdinalIgnoreCase))
            .Select(node => new PullRequestReviewResponse(
                node.Name,
                node.Status,
                node.ExternalReference))
            .OrderBy(x => x.Reviewer, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var ciIds = graph.Edges
            .Where(edge =>
                commitIds.Contains(edge.SourceNodeId)
                && edge.Type.Equals(DevelopmentGraphEdgeType.BuiltBy.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(edge => edge.TargetNodeId)
            .ToHashSet();
        var ciRuns = ciIds
            .Where(byId.ContainsKey)
            .Select(id => byId[id])
            .Where(node => node.Type.Equals(DevelopmentGraphNodeType.CiRun.ToString(), StringComparison.OrdinalIgnoreCase))
            .Select(node => new PullRequestCiResponse(
                node.Name,
                node.Status,
                node.ExternalReference))
            .OrderByDescending(x => x.Status.Equals("failure", StringComparison.OrdinalIgnoreCase))
            .ThenBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        var critical = reviewEdges.Any(edge => edge.Critical)
                       || ciRuns.Any(run => IsFailure(run.Status))
                       || assessment?.Status is "Blocked" or "HighRisk";
        var title = PullRequestTitle(pullRequest.Name, number.Value);
        var summary = new PullRequestSummaryResponse(
            number.Value,
            title,
            pullRequest.Status,
            pullRequest.ExternalReference,
            changedFiles.Length,
            reviews.Length,
            ciRuns.Length,
            critical,
            assessment?.Status);

        return new(
            summary,
            revisions,
            changedFiles,
            reviews,
            ciRuns,
            Array.Empty<PullRequestPipelineResponse>(),
            Array.Empty<PipelineQualityIssueResponse>(),
            Array.Empty<PipelineEvidenceResponse>());
    }

    private async Task<PullRequestDetailsResponse> EnrichPullRequestDetailsAsync(
            Guid projectId,
            PullRequestDetailsResponse details,
            CancellationToken cancellationToken)
    {
        if (details.Revisions.Count == 0)
            return details;

        var runs = await pipelineRuns.ListByCommitShasAsync(projectId, details.Revisions, cancellationToken);
        var runIds = runs.Select(run => run.Id).ToArray();
        var issues = await qualityIssues.ListByPipelineRunsAsync(projectId, runIds, cancellationToken);
        var evidenceRecords = await evidence.ListByPipelineRunsAsync(projectId, runIds, cancellationToken);

        var issueDtos = issues
            .GroupBy(issue => issue.Id)
            .Select(group => group.First())
            .OrderByDescending(issue => issue.Severity)
            .ThenByDescending(issue => issue.LastSeenAtUtc)
            .Select(issue => new PipelineQualityIssueResponse(
                issue.Id,
                issue.Severity.ToString(),
                issue.Status.ToString(),
                issue.Title,
                issue.Category,
                issue.Fingerprint,
                issue.EvidenceJson))
            .ToArray();
        var evidenceDtos = evidenceRecords
            .GroupBy(item => item.Id)
            .Select(group => group.First())
            .OrderByDescending(item => item.ObservedAtUtc)
            .Select(item => new PipelineEvidenceResponse(
                item.Id,
                item.EvidenceKind,
                item.Outcome.ToString(),
                item.Title,
                item.Summary,
                item.Confidence.ToString(),
                item.SourceReference,
                item.ObservedAtUtc))
            .ToArray();
        var pipelineDtos = runs
            .Select(run => new PullRequestPipelineResponse(
                run.Id,
                run.CommitSha.Value,
                run.Status.ToString(),
                run.CollectorType,
                run.ExternalRunId,
                run.FinishedAtUtc,
                issues.Count(issue => issue.PipelineRunId == run.Id
                                      || issue.FirstSeenPipelineRunId == run.Id
                                      || issue.LastSeenPipelineRunId == run.Id),
                evidenceRecords.Count(item => item.PipelineRunId == run.Id)))
            .ToArray();

        return details with
        {
            PipelineRuns = pipelineDtos,
            QualityIssues = issueDtos,
            Evidence = evidenceDtos
        };
    }

    private static bool IsFailure(string value)
            => value.Equals("failure", StringComparison.OrdinalIgnoreCase)
               || value.Equals("failed", StringComparison.OrdinalIgnoreCase)
               || value.Equals("error", StringComparison.OrdinalIgnoreCase)
               || value.Equals("cancelled", StringComparison.OrdinalIgnoreCase);

    private static int? PullRequestNumber(DevelopmentGraphNodeDto node)
    {
        var metadata = MetaInt(node.MetadataJson, "PullRequestNumber");
        if (metadata.HasValue)
            return metadata;
        var tail = StripKeyTail(node.Key);
        return int.TryParse(tail, out var number) ? number : null;
    }

    private static string PullRequestTitle(string value, int number)
    {
        var prefix = $"PR #{number}:";
        if (value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return value[prefix.Length..].Trim();
        return value;
    }

    private static int? MetaInt(string json, string property)
    {
        if (!TryParseMetadata(json, out var document) || document is null)
            return null;

        using (document)
        {
            if (document.RootElement.TryGetProperty(property, out var value)
                && value.TryGetInt32(out var result))
            {
                return result;
            }
        }
        return null;
    }

    private static string? MetaString(string json, string property)
    {
        if (!TryParseMetadata(json, out var document) || document is null)
            return null;

        using (document)
        {
            if (document.RootElement.TryGetProperty(property, out var value))
                return value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText();
        }
        return null;
    }

    private static bool TryParseMetadata(string json, out JsonDocument? document)
    {
        try
        {
            document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            return true;
        }
        catch (JsonException)
        {
            // Malformed graph metadata is intentionally treated as unavailable.
            // Callers can fall back to stable node keys instead of silently
            // assuming malformed JSON was valid metadata.
            document = null;
            return false;
        }
    }

    private static string StripKeyTail(string key)
    {
        var index = key.LastIndexOf(':');
        return index >= 0 && index + 1 < key.Length ? key[(index + 1)..] : key;
    }
}
