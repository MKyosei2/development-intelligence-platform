// Purpose: Coordinates P ro je ct Ac ce ss Se rv ic e application behavior for Authorization.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Authorization;

public sealed record ProjectMembershipDto(
    Guid Id,
    Guid ProjectId,
    string SubjectId,
    string Login,
    ProjectMembershipRole Role,
    string GrantedBy,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed class ProjectAccessService(
    IProjectMembershipRepository memberships,
    IAuditLogRepository audit,
    IUnitOfWork uow,
    ISystemClock clock,
    IProjectMutationCoordinator mutations)
{
    public async Task<bool> HasAccessAsync(
        Guid projectId,
        string subjectId,
        ProjectMembershipRole minimum,
        CancellationToken ct)
    {
        var membership = await memberships.FindAsync(projectId, subjectId, ct);
        return membership is not null && Rank(membership.Role) >= Rank(minimum);
    }

    public async Task<IReadOnlySet<Guid>> AccessibleProjectsAsync(
        string subjectId,
        CancellationToken ct)
        => (await memberships.ListBySubjectAsync(subjectId, ct))
            .Select(x => x.ProjectId)
            .ToHashSet();

    public Task GrantOwnerAsync(
        Guid projectId,
        string subjectId,
        string login,
        string actor,
        CancellationToken ct)
        => UpsertAsync(
            projectId,
            subjectId,
            login,
            ProjectMembershipRole.Owner,
            actor,
            "Initial project owner",
            ct);

    public Task UpsertAsync(
        Guid projectId,
        string subjectId,
        string login,
        ProjectMembershipRole role,
        string actor,
        string reason,
        CancellationToken ct)
        => mutations.ExecuteAsync(projectId, async token =>
        {
            var existing = await memberships.FindAsync(projectId, subjectId, token);
            var before = existing is null
                ? null
                : JsonSerializer.Serialize(new
                {
                    existing.SubjectId,
                    existing.Login,
                    existing.Role
                });

            if (existing is null)
            {
                existing = new ProjectMembership(
                    Guid.NewGuid(),
                    projectId,
                    subjectId,
                    login,
                    role,
                    actor,
                    clock.UtcNow);
                await memberships.AddAsync(existing, token);
            }
            else
            {
                if (existing.Role == ProjectMembershipRole.Owner
                    && role != ProjectMembershipRole.Owner)
                {
                    var all = await memberships.ListByProjectAsync(projectId, token);
                    if (all.Count(x => x.Role == ProjectMembershipRole.Owner) <= 1)
                    {
                        throw new InvalidOperationException(
                            "The last project owner cannot be demoted. Transfer ownership first.");
                    }
                }

                existing.ChangeRole(role, actor, clock.UtcNow);
            }

            await audit.AddAsync(
                new AuditLog(
                    Guid.NewGuid(),
                    projectId,
                    "authorization",
                    "membership.upsert",
                    actor,
                    subjectId,
                    JsonSerializer.Serialize(new
                    {
                        before,
                        after = new
                        {
                            existing.SubjectId,
                            existing.Login,
                            existing.Role
                        }
                    }),
                    clock.UtcNow,
                    reason: reason),
                token);
            await uow.SaveChangesAsync(token);
        }, ct);

    public Task RemoveAsync(
        Guid projectId,
        string subjectId,
        string actor,
        string reason,
        CancellationToken ct)
        => mutations.ExecuteAsync(projectId, async token =>
        {
            var existing = await memberships.FindAsync(projectId, subjectId, token)
                ?? throw new KeyNotFoundException("Project membership not found.");

            if (existing.Role == ProjectMembershipRole.Owner)
            {
                var all = await memberships.ListByProjectAsync(projectId, token);
                if (all.Count(x => x.Role == ProjectMembershipRole.Owner) <= 1)
                    throw new InvalidOperationException("A project must retain at least one owner.");
            }

            memberships.Remove(existing);
            await audit.AddAsync(
                new AuditLog(
                    Guid.NewGuid(),
                    projectId,
                    "authorization",
                    "membership.remove",
                    actor,
                    subjectId,
                    JsonSerializer.Serialize(new
                    {
                        existing.SubjectId,
                        existing.Login,
                        existing.Role
                    }),
                    clock.UtcNow,
                    reason: reason),
                token);
            await uow.SaveChangesAsync(token);
        }, ct);

    public Task TransferOwnershipAsync(
        Guid projectId,
        string currentActorSubject,
        string newOwnerSubject,
        string newOwnerLogin,
        string actor,
        string reason,
        CancellationToken ct)
        => mutations.ExecuteAsync(projectId, async token =>
        {
            var all = await memberships.ListByProjectAsync(projectId, token);
            var currentOwnerSnapshot = all.FirstOrDefault(x =>
                    x.SubjectId.Equals(currentActorSubject, StringComparison.Ordinal)
                    && x.Role == ProjectMembershipRole.Owner)
                ?? all.FirstOrDefault(x => x.Role == ProjectMembershipRole.Owner)
                ?? throw new InvalidOperationException("The project has no current owner.");

            var currentOwner = await memberships.FindAsync(
                projectId,
                currentOwnerSnapshot.SubjectId,
                token)
                ?? throw new InvalidOperationException("The current owner membership could not be loaded.");

            var newOwner = await memberships.FindAsync(projectId, newOwnerSubject, token);
            if (newOwner is null)
            {
                newOwner = new ProjectMembership(
                    Guid.NewGuid(),
                    projectId,
                    newOwnerSubject,
                    newOwnerLogin,
                    ProjectMembershipRole.Owner,
                    actor,
                    clock.UtcNow);
                await memberships.AddAsync(newOwner, token);
            }
            else
            {
                newOwner.ChangeRole(ProjectMembershipRole.Owner, actor, clock.UtcNow);
            }

            if (!currentOwner.SubjectId.Equals(newOwnerSubject, StringComparison.Ordinal))
                currentOwner.ChangeRole(ProjectMembershipRole.Maintainer, actor, clock.UtcNow);

            await audit.AddAsync(
                new AuditLog(
                    Guid.NewGuid(),
                    projectId,
                    "authorization",
                    "membership.owner.transfer",
                    actor,
                    newOwnerSubject,
                    JsonSerializer.Serialize(new
                    {
                        previousOwner = new
                        {
                            currentOwner.SubjectId,
                            currentOwner.Login
                        },
                        newOwner = new
                        {
                            newOwner.SubjectId,
                            newOwner.Login
                        }
                    }),
                    clock.UtcNow,
                    reason: reason),
                token);
            await uow.SaveChangesAsync(token);
        }, ct);

    public async Task<IReadOnlyList<ProjectMembershipDto>> ListAsync(
        Guid projectId,
        CancellationToken ct)
        => (await memberships.ListByProjectAsync(projectId, ct))
            .Select(Map)
            .ToArray();

    private static int Rank(ProjectMembershipRole role) => role switch
    {
        ProjectMembershipRole.Viewer => 0,
        ProjectMembershipRole.Member => 1,
        ProjectMembershipRole.Maintainer => 2,
        ProjectMembershipRole.Owner => 3,
        _ => -1
    };

    private static ProjectMembershipDto Map(ProjectMembership membership)
        => new(
            membership.Id,
            membership.ProjectId,
            membership.SubjectId,
            membership.Login,
            membership.Role,
            membership.GrantedBy,
            membership.CreatedAtUtc,
            membership.UpdatedAtUtc);
}
