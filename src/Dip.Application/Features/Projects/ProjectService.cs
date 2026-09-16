// Purpose: Coordinates P ro je ct Se rv ic e application behavior for Projects.
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Projects;

public sealed record CreateProjectCommand(
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    DateTimeOffset? ReleaseDateUtc);

public sealed record UpdateProjectCommand(
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    DateTimeOffset? ReleaseDateUtc,
    string Reason = "Project settings updated from the DIP UI.");

public sealed record ProjectLifecycleCommand(
    string Reason = "Project lifecycle changed from the DIP UI.");

public sealed record ProjectDto(
    Guid Id,
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    decimal HealthScore,
    DateTimeOffset? ReleaseDateUtc,
    string LifecycleState = "Active",
    DateTimeOffset? LifecycleChangedAtUtc = null);

public sealed record ProjectAuditEntryDto(
    Guid Id,
    Guid ProjectId,
    string Category,
    string Action,
    string Actor,
    string? SubjectKey,
    string? DetailJson,
    string? RequestId,
    string? CorrelationId,
    string? Reason,
    DateTimeOffset CreatedAtUtc);

public sealed record ProjectAuditPageDto(
    IReadOnlyList<ProjectAuditEntryDto> Items,
    string? NextCursor,
    bool HasMore);

public sealed class ProjectService(
    IProjectRepository projects,
    IProjectMembershipRepository memberships,
    IAuditLogRepository audit,
    IUnitOfWork uow,
    ISystemClock clock)
{
    public const string ActiveState = "Active";
    public const string ArchivedState = "Archived";
    public const string DeletedState = "Deleted";

    private const string LifecycleCategory = "project-lifecycle";

    public async Task<ProjectDto> CreateAsync(
        CreateProjectCommand command,
        string ownerSubject,
        string ownerLogin,
        string actor,
        CancellationToken ct)
    {
        var project = new Project(
            Guid.NewGuid(),
            command.Name,
            command.RepositoryUrl,
            command.DefaultBranch,
            clock.UtcNow);
        project.SetReleaseDate(command.ReleaseDateUtc);
        await projects.AddAsync(project, ct);

        var membership = new ProjectMembership(
            Guid.NewGuid(),
            project.Id,
            ownerSubject,
            ownerLogin,
            ProjectMembershipRole.Owner,
            actor,
            clock.UtcNow);
        await memberships.AddAsync(membership, ct);

        await audit.AddAsync(
            new AuditLog(
                Guid.NewGuid(),
                project.Id,
                "authorization",
                "project.provision",
                actor,
                project.Id.ToString(),
                JsonSerializer.Serialize(new
                {
                    ownerSubject,
                    ownerLogin,
                    role = ProjectMembershipRole.Owner.ToString()
                }),
                clock.UtcNow,
                reason: "Project created with an explicit owner membership."),
            ct);

        await uow.SaveChangesAsync(ct);
        return Map(project, ActiveState, null);
    }

    public async Task<IReadOnlyList<ProjectDto>> ListAsync(CancellationToken ct)
    {
        var entities = await projects.ListAllAsync(ct);
        var result = new List<ProjectDto>(entities.Count);
        foreach (var project in entities)
        {
            var lifecycle = await ResolveLifecycleAsync(project.Id, ct);
            result.Add(Map(project, lifecycle.State, lifecycle.ChangedAtUtc));
        }
        return result;
    }

    public async Task<ProjectDto?> GetAsync(Guid id, CancellationToken ct)
    {
        var project = await projects.GetByIdAsync(id, ct);
        if (project is null)
            return null;

        var lifecycle = await ResolveLifecycleAsync(id, ct);
        return Map(project, lifecycle.State, lifecycle.ChangedAtUtc);
    }

    public async Task<string> GetLifecycleStateAsync(Guid id, CancellationToken ct)
        => (await ResolveLifecycleAsync(id, ct)).State;

    public async Task<ProjectDto> UpdateAsync(
        Guid id,
        UpdateProjectCommand command,
        string actor,
        CancellationToken ct)
    {
        var project = await NeedProjectAsync(id, ct);
        await EnsureActiveAsync(id, ct);

        var before = new
        {
            project.Name,
            project.RepositoryUrl,
            project.DefaultBranch,
            project.ReleaseDateUtc
        };

        project.UpdateDetails(
            command.Name,
            command.RepositoryUrl,
            command.DefaultBranch,
            command.ReleaseDateUtc,
            clock.UtcNow);

        await audit.AddAsync(
            new AuditLog(
                Guid.NewGuid(),
                id,
                "project",
                "project.updated",
                actor,
                id.ToString(),
                JsonSerializer.Serialize(new
                {
                    before,
                    after = new
                    {
                        project.Name,
                        project.RepositoryUrl,
                        project.DefaultBranch,
                        project.ReleaseDateUtc
                    }
                }),
                clock.UtcNow,
                reason: CleanReason(command.Reason)),
            ct);

        await uow.SaveChangesAsync(ct);
        return Map(project, ActiveState, null);
    }

    public Task<ProjectDto> ArchiveAsync(
        Guid id,
        string actor,
        string reason,
        CancellationToken ct)
        => ChangeLifecycleAsync(id, ArchivedState, "project.archived", actor, reason, ct);

    public Task<ProjectDto> RestoreAsync(
        Guid id,
        string actor,
        string reason,
        CancellationToken ct)
        => ChangeLifecycleAsync(id, ActiveState, "project.restored", actor, reason, ct);

    public Task<ProjectDto> DeleteAsync(
        Guid id,
        string actor,
        string reason,
        CancellationToken ct)
        => ChangeLifecycleAsync(id, DeletedState, "project.deleted", actor, reason, ct);

    public Task<IReadOnlyList<ProjectAuditEntryDto>> GetAuditAsync(
        Guid projectId,
        int take,
        CancellationToken ct)
        => GetAuditAsync(projectId, take, null, null, null, null, null, ct);

    public async Task<IReadOnlyList<ProjectAuditEntryDto>> GetAuditAsync(
        Guid projectId,
        int take,
        string? search,
        string? category,
        string? correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        CancellationToken ct)
    {
        _ = await NeedProjectAsync(projectId, ct);
        var entries = await audit.SearchAsync(
            projectId,
            search,
            category,
            correlationId,
            fromUtc,
            toUtc,
            Math.Clamp(take, 1, 5000),
            ct);
        return entries.Select(MapAudit).ToArray();
    }

    public async Task<ProjectAuditPageDto> GetAuditPageAsync(
        Guid projectId,
        int take,
        string? search,
        string? category,
        string? correlationId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string? cursor,
        CancellationToken ct)
    {
        _ = await NeedProjectAsync(projectId, ct);
        var (beforeUtc, beforeId) = DecodeAuditCursor(cursor);
        var page = await audit.SearchPageAsync(
            projectId,
            search,
            category,
            correlationId,
            fromUtc,
            toUtc,
            beforeUtc,
            beforeId,
            Math.Clamp(take, 1, 500),
            ct);
        var nextCursor = page.NextBeforeUtc.HasValue && page.NextBeforeId.HasValue
            ? EncodeAuditCursor(page.NextBeforeUtc.Value, page.NextBeforeId.Value)
            : null;
        return new ProjectAuditPageDto(
            page.Items.Select(MapAudit).ToArray(),
            nextCursor,
            nextCursor is not null);
    }

    private async Task<ProjectDto> ChangeLifecycleAsync(
        Guid id,
        string nextState,
        string action,
        string actor,
        string reason,
        CancellationToken ct)
    {
        var project = await NeedProjectAsync(id, ct);
        var current = await ResolveLifecycleAsync(id, ct);

        if (nextState == ArchivedState && current.State != ActiveState)
            throw new InvalidOperationException($"Only an active project can be archived. Current state: {current.State}.");
        if (nextState == ActiveState && current.State != ArchivedState)
            throw new InvalidOperationException($"Only an archived project can be restored. Current state: {current.State}.");
        if (nextState == DeletedState && current.State == DeletedState)
            return Map(project, current.State, current.ChangedAtUtc);

        var changedAt = clock.UtcNow;
        if (current.ChangedAtUtc is { } previous && changedAt <= previous)
            changedAt = previous.AddTicks(1);

        await audit.AddAsync(
            new AuditLog(
                Guid.NewGuid(),
                id,
                LifecycleCategory,
                action,
                actor,
                id.ToString(),
                JsonSerializer.Serialize(new
                {
                    previousState = current.State,
                    nextState
                }),
                changedAt,
                reason: CleanReason(reason)),
            ct);

        await uow.SaveChangesAsync(ct);
        return Map(project, nextState, changedAt);
    }

    private async Task EnsureActiveAsync(Guid projectId, CancellationToken ct)
    {
        var lifecycle = await ResolveLifecycleAsync(projectId, ct);
        if (lifecycle.State != ActiveState)
            throw new InvalidOperationException($"Project is {lifecycle.State} and cannot be modified.");
    }

    private async Task<Project> NeedProjectAsync(Guid id, CancellationToken ct)
        => await projects.GetByIdAsync(id, ct)
           ?? throw new KeyNotFoundException($"Project '{id}' was not found.");

    private async Task<(string State, DateTimeOffset? ChangedAtUtc)> ResolveLifecycleAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var latest = await audit.GetLatestByCategoryAsync(projectId, LifecycleCategory, ct);

        if (latest is null || latest.Action is not ("project.archived" or "project.restored" or "project.deleted"))
            return (ActiveState, null);

        var state = latest.Action switch
        {
            "project.archived" => ArchivedState,
            "project.deleted" => DeletedState,
            _ => ActiveState
        };
        return (state, latest.CreatedAtUtc);
    }

    private static ProjectDto Map(
        Project project,
        string lifecycleState,
        DateTimeOffset? lifecycleChangedAtUtc)
        => new(
            project.Id,
            project.Name,
            project.RepositoryUrl,
            project.DefaultBranch,
            project.HealthScore.Value,
            project.ReleaseDateUtc,
            lifecycleState,
            lifecycleChangedAtUtc);

    private static ProjectAuditEntryDto MapAudit(AuditLog entry)
        => new(
            entry.Id,
            entry.ProjectId,
            entry.Category,
            entry.Action,
            entry.Actor,
            entry.SubjectKey,
            entry.DetailJson,
            entry.RequestId,
            entry.CorrelationId,
            entry.Reason,
            entry.CreatedAtUtc);

    private static string EncodeAuditCursor(DateTimeOffset createdAtUtc, Guid id)
    {
        var raw = $"{createdAtUtc.UtcDateTime.Ticks}|{id:N}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(raw))
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static (DateTimeOffset? BeforeUtc, Guid? BeforeId) DecodeAuditCursor(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return (null, null);
        try
        {
            var normalized = cursor.Trim().Replace('-', '+').Replace('_', '/');
            normalized += (normalized.Length % 4) switch { 2 => "==", 3 => "=", _ => string.Empty };
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(normalized));
            var parts = raw.Split('|', 2);
            if (parts.Length != 2
                || !long.TryParse(parts[0], out var ticks)
                || !Guid.TryParseExact(parts[1], "N", out var id))
            {
                throw new ArgumentException("Audit cursor is invalid.", nameof(cursor));
            }
            return (new DateTimeOffset(new DateTime(ticks, DateTimeKind.Utc)), id);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Audit cursor is invalid.", nameof(cursor), exception);
        }
    }

    private static string CleanReason(string? reason)
    {
        var value = reason?.Trim();
        return string.IsNullOrWhiteSpace(value)
            ? "Project change requested from the DIP UI."
            : value.Length <= 2000 ? value : value[..2000];
    }
}
