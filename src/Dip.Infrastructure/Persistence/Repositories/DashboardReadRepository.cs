using Dip.Application.Features.Dashboard;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

public sealed class DashboardReadRepository : IDashboardReadRepository
{
    private readonly DipDbContext _db;

    public DashboardReadRepository(DipDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProjectDashboardSummary>> GetProjectsAsync(
        CancellationToken cancellationToken)
    {
        var projects = await _db.Projects
            .AsNoTracking()
            .OrderBy(x => x.Name)
            .ToListAsync(cancellationToken);

        var results = new List<ProjectDashboardSummary>(projects.Count);

        foreach (var project in projects)
        {
            var latestRun = await _db.PipelineRuns
                .AsNoTracking()
                .Where(x => x.ProjectId == project.Id)
                .OrderByDescending(x => x.FinishedAtUtc ?? x.StartedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var latestGate = await _db.Set<QualityGateResult>()
                .AsNoTracking()
                .Where(x => x.ProjectId == project.Id)
                .OrderByDescending(x => x.EvaluatedAtUtc)
                .FirstOrDefaultAsync(cancellationToken);

            var score = latestGate?.Score
                        ?? project.HealthScore.Value;

            results.Add(new(
                project.Id,
                project.Name,
                project.RepositoryUrl,
                project.DefaultBranch,
                score,
                Risk(score),
                latestRun?.Status.ToString(),
                latestGate?.Status,
                latestGate?.EvaluatedAtUtc));
        }

        return results;
    }

    public async Task<ProjectDashboardDetails?> GetProjectAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var project = await _db.Projects
            .AsNoTracking()
            .SingleOrDefaultAsync(
                x => x.Id == projectId,
                cancellationToken);

        if (project is null)
            return null;

        var latestRun = await _db.PipelineRuns
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.FinishedAtUtc ?? x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var latestGate = await _db.Set<QualityGateResult>()
            .AsNoTracking()
            .Where(x => x.ProjectId == projectId)
            .OrderByDescending(x => x.EvaluatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        var metrics = latestRun is null
            ? new List<DashboardMetricPoint>()
            : await _db.Metrics
                .AsNoTracking()
                .Where(x => x.PipelineRunId == latestRun.Id)
                .OrderBy(x => x.Name)
                .Select(x => new DashboardMetricPoint(
                    x.Name,
                    x.Value,
                    x.Unit,
                    x.DifferencePercent))
                .ToListAsync(cancellationToken);

        var issues = await _db.QualityIssues
            .AsNoTracking()
            .Where(x =>
                x.ProjectId == projectId
                && x.Status == IssueStatus.Open)
            .OrderByDescending(x => x.Severity)
            .ThenBy(x => x.Title)
            .Take(50)
            .Select(x => new DashboardIssue(
                x.Id,
                x.Severity.ToString(),
                x.Category,
                x.Title,
                x.Status.ToString()))
            .ToListAsync(cancellationToken);

        var score = latestGate?.Score
                    ?? project.HealthScore.Value;

        return new(
            project.Id,
            project.Name,
            project.RepositoryUrl,
            project.DefaultBranch,
            score,
            Risk(score),
            latestRun?.Id,
            latestRun?.Status.ToString(),
            latestRun?.FinishedAtUtc,
            latestGate?.Status,
            latestGate?.Score,
            latestGate?.CriticalIssueCount ?? 0,
            latestGate?.WarningIssueCount ?? 0,
            latestGate?.GetReasons() ?? Array.Empty<string>(),
            metrics,
            issues);
    }

    private static string Risk(decimal score) =>
        score < 60m ? "High"
        : score < 85m ? "Medium"
        : "Low";
}
