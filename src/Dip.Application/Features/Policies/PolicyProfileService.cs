// Purpose: Manages reusable project policy profiles and their validated configuration.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Policies;

public sealed class PolicyProfileService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPolicyProfileRepository _profiles;
    private readonly IProjectRepository _projects;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public PolicyProfileService(
        IPolicyProfileRepository profiles,
        IProjectRepository projects,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _profiles = profiles;
        _projects = projects;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PolicyProfileDto?> GetGlobalAsync(CancellationToken ct)
    {
        var profile = await _profiles.GetGlobalAsync(ct);
        return profile is null ? null : Map(profile);
    }

    public async Task<PolicyProfileDto?> GetProjectAsync(
        Guid projectId,
        CancellationToken ct)
    {
        var profile = await _profiles.GetProjectAsync(projectId, ct);
        return profile is null ? null : Map(profile);
    }

    public async Task<PolicyProfileDto> UpsertGlobalAsync(
        PolicyProfileDocument document,
        CancellationToken ct)
    {
        Validate(document);
        var profile = await _profiles.GetGlobalAsync(ct);
        var now = _clock.UtcNow;

        if (profile is null)
        {
            profile = Create(
                PolicyScopeType.Global,
                null,
                null,
                document,
                now);
            await _profiles.AddAsync(profile, ct);
        }
        else
        {
            Update(profile, document, now);
        }

        await _uow.SaveChangesAsync(ct);
        return Map(profile);
    }

    public async Task<PolicyProfileDto> UpsertProjectAsync(
        Guid projectId,
        PolicyProfileDocument document,
        CancellationToken ct)
    {
        if (projectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(projectId));
        if (await _projects.GetByIdAsync(projectId, ct) is null)
            throw new KeyNotFoundException($"Project {projectId} was not found.");

        Validate(document);
        var profile = await _profiles.GetProjectAsync(projectId, ct);
        var now = _clock.UtcNow;

        if (profile is null)
        {
            profile = Create(
                PolicyScopeType.Project,
                projectId,
                null,
                document,
                now);
            await _profiles.AddAsync(profile, ct);
        }
        else
        {
            Update(profile, document, now);
        }

        await _uow.SaveChangesAsync(ct);
        return Map(profile);
    }

    private static PolicyProfile Create(
        PolicyScopeType scope,
        Guid? projectId,
        string? agentId,
        PolicyProfileDocument document,
        DateTimeOffset now)
        => new(
            Guid.NewGuid(),
            scope,
            projectId,
            agentId,
            document.Name,
            JsonSerializer.Serialize(document.Evidence, JsonOptions),
            JsonSerializer.Serialize(document.Agent, JsonOptions),
            JsonSerializer.Serialize(document.Retention, JsonOptions),
            JsonSerializer.Serialize(document.Alert, JsonOptions),
            JsonSerializer.Serialize(document.Privacy, JsonOptions),
            document.QaPolicyId,
            document.Enabled,
            now);

    private static void Update(
        PolicyProfile profile,
        PolicyProfileDocument document,
        DateTimeOffset now)
        => profile.Update(
            document.Name,
            JsonSerializer.Serialize(document.Evidence, JsonOptions),
            JsonSerializer.Serialize(document.Agent, JsonOptions),
            JsonSerializer.Serialize(document.Retention, JsonOptions),
            JsonSerializer.Serialize(document.Alert, JsonOptions),
            JsonSerializer.Serialize(document.Privacy, JsonOptions),
            document.QaPolicyId,
            document.Enabled,
            now);

    private static PolicyProfileDto Map(PolicyProfile profile)
        => new(
            profile.Id,
            profile.ScopeType,
            profile.ProjectId,
            profile.AgentId,
            profile.Name,
            profile.Version,
            profile.Enabled,
            PolicyResolver.Read(profile),
            profile.UpdatedAtUtc);

    private static void Validate(PolicyProfileDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        if (string.IsNullOrWhiteSpace(document.Name))
            throw new ArgumentException("Policy name is required.", nameof(document));
        ValidatePositive(document.Retention.MetadataRetentionDays, "MetadataRetentionDays");
        ValidatePositive(document.Retention.ArtifactRetentionDays, "ArtifactRetentionDays");
        ValidatePositive(document.Retention.VideoRetentionDays, "VideoRetentionDays");
        ValidatePositive(document.Alert.CriticalThreshold, "CriticalThreshold");
    }

    private static void ValidatePositive(int? value, string name)
    {
        if (value.HasValue && value.Value <= 0)
            throw new ArgumentOutOfRangeException(name, "Value must be positive.");
    }
}
