// Purpose: Defines stable GitHub integration API response contracts shared by focused controllers.
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

public sealed record GitHubCapabilityCheckResponse(
    string Name,
    string Status,
    string Message);

public sealed record GitHubConnectionTestResponse(
    bool Success,
    string Mode,
    string Message,
    int RepositoryCount,
    DateTimeOffset TestedAtUtc,
    string OverallStatus = "Unknown",
    string? Repository = null,
    IReadOnlyList<GitHubCapabilityCheckResponse>? Checks = null);

public sealed record PullRequestSummaryResponse(
    int Number,
    string Title,
    string Status,
    string? Url,
    int ChangedFiles,
    int Reviews,
    int CiRuns,
    bool Critical,
    string? ReviewState);

public sealed record PullRequestReviewResponse(
    string Reviewer,
    string State,
    string? Url);

public sealed record PullRequestCiResponse(
    string Name,
    string Status,
    string? Url);

public sealed record PullRequestPipelineResponse(
    Guid Id,
    string CommitSha,
    string Status,
    string CollectorType,
    string ExternalRunId,
    DateTimeOffset? FinishedAtUtc,
    int QualityIssueCount,
    int EvidenceCount);

public sealed record PullRequestDetailsResponse(
    PullRequestSummaryResponse Summary,
    IReadOnlyList<string> Revisions,
    IReadOnlyList<string> ChangedFiles,
    IReadOnlyList<PullRequestReviewResponse> Reviews,
    IReadOnlyList<PullRequestCiResponse> CiRuns,
    IReadOnlyList<PullRequestPipelineResponse> PipelineRuns,
    IReadOnlyList<PipelineQualityIssueResponse> QualityIssues,
    IReadOnlyList<PipelineEvidenceResponse> Evidence);
