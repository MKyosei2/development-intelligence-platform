// Purpose: Coordinates P ro je ct Cl ai mS er vi ce application behavior for ProjectTruth.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;

namespace Dip.Application.Features.ProjectTruth;

public sealed record CreateProjectClaimCommand(
    Guid ProjectId,
    string SubjectKey,
    string ClaimedStatus,
    decimal? ClaimedProgress,
    string ClaimedBy,
    string? Description);

public sealed record ProjectClaimDto(
    Guid Id,
    Guid ProjectId,
    string SubjectKey,
    string ClaimedStatus,
    decimal? ClaimedProgress,
    string ClaimedBy,
    string? Description,
    string VerificationStatus,
    DateTimeOffset ClaimedAtUtc,
    DateTimeOffset? LastVerifiedAtUtc);

public sealed class ProjectClaimService
{
    private readonly IProjectRepository _projects;
    private readonly IProjectClaimRepository _claims;
    private readonly IAuditLogRepository _audit;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ISystemClock _clock;
    private readonly ProjectTruthService _truth;

    public ProjectClaimService(
        IProjectRepository projects,
        IProjectClaimRepository claims,
        IAuditLogRepository audit,
        IUnitOfWork unitOfWork,
        ISystemClock clock,
        ProjectTruthService truth)
    {
        _projects = projects;
        _claims = claims;
        _audit = audit;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _truth = truth;
    }

    public async Task<ProjectClaimDto> CreateAsync(
        CreateProjectClaimCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _ = await _projects.GetByIdAsync(command.ProjectId, cancellationToken)
            ?? throw new KeyNotFoundException($"Project {command.ProjectId} not found.");

        var claim = new ProjectClaim(
            Guid.NewGuid(),
            command.ProjectId,
            command.SubjectKey,
            command.ClaimedStatus,
            command.ClaimedProgress,
            command.ClaimedBy,
            command.Description,
            _clock.UtcNow);

        await _claims.AddAsync(claim, cancellationToken);
        await _audit.AddAsync(
            new AuditLog(
                Guid.NewGuid(),
                command.ProjectId,
                "claim",
                "submitted",
                command.ClaimedBy,
                command.SubjectKey,
                JsonSerializer.Serialize(new
                {
                    command.ClaimedStatus,
                    command.ClaimedProgress,
                    command.Description
                }),
                _clock.UtcNow),
            cancellationToken);

        // Persist the claim first so the truth service can evaluate the same
        // durable data that the dashboard will later read.
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        await _truth.RecalculateAsync(command.ProjectId, cancellationToken);

        return Map(claim);
    }

    public async Task<IReadOnlyList<ProjectClaimDto>> ListAsync(
        Guid projectId,
        CancellationToken cancellationToken)
    {
        var claims = await _claims.ListByProjectAsync(projectId, cancellationToken);
        return claims
            .OrderByDescending(item => item.ClaimedAtUtc)
            .Select(Map)
            .ToArray();
    }

    private static ProjectClaimDto Map(ProjectClaim claim) => new(
        claim.Id,
        claim.ProjectId,
        claim.SubjectKey,
        claim.ClaimedStatus,
        claim.ClaimedProgress,
        claim.ClaimedBy,
        claim.Description,
        claim.VerificationStatus.ToString(),
        claim.ClaimedAtUtc,
        claim.LastVerifiedAtUtc);
}
