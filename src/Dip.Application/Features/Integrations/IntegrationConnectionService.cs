// Purpose: Manages external integration connections, credentials metadata, and connection validation.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Security;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Evidence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Integrations;

public sealed class IntegrationConnectionService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IIntegrationConnectionRepository _connections;
    private readonly IProjectSourceBindingRepository _bindings;
    private readonly IProjectRepository _projects;
    private readonly IIntegrationCredentialStore _credentials;
    private readonly IntegrationConnectorRegistry _connectors;
    private readonly EvidenceIngestionService _evidence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;

    public IntegrationConnectionService(
        IIntegrationConnectionRepository connections,
        IProjectSourceBindingRepository bindings,
        IProjectRepository projects,
        IIntegrationCredentialStore credentials,
        IntegrationConnectorRegistry connectors,
        EvidenceIngestionService evidence,
        IUnitOfWork uow,
        ISystemClock clock)
    {
        _connections = connections;
        _bindings = bindings;
        _projects = projects;
        _credentials = credentials;
        _connectors = connectors;
        _evidence = evidence;
        _uow = uow;
        _clock = clock;
    }

    public async Task<IReadOnlyList<IntegrationConnectionDto>> ListAsync(
        CancellationToken ct)
        => (await _connections.ListAsync(ct)).Select(Map).ToArray();

    public async Task<IntegrationConnectionDto?> GetAsync(
        Guid id,
        CancellationToken ct)
    {
        var connection = await _connections.GetAsync(id, ct);
        return connection is null ? null : Map(connection);
    }

    public async Task<IntegrationConnectionDto> CreateAsync(
        CreateIntegrationConnectionCommand command,
        CancellationToken ct)
    {
        ValidateCreate(command);

        var secretJson = JsonSerializer.Serialize(command.Credential, JsonOptions);
        var credentialReference = await _credentials.StoreAsync(
            command.Provider,
            secretJson,
            ct);

        var connection = new IntegrationConnection(
            Guid.NewGuid(),
            command.Provider,
            command.DisplayName,
            command.Mode,
            NormalizeBaseUrl(command.Mode, command.BaseUrl),
            credentialReference,
            _clock.UtcNow);

        try
        {
            await _connections.AddAsync(connection, ct);
            await _uow.SaveChangesAsync(ct);
        }
        catch
        {
            await _credentials.DeleteAsync(credentialReference, ct);
            throw;
        }

        if (command.TestAfterSave)
            return await TestAsync(connection.Id, ct);

        return Map(connection);
    }

    public async Task<IntegrationConnectionDto> TestAsync(
        Guid connectionId,
        CancellationToken ct)
    {
        var connection = await RequiredConnectionAsync(connectionId, ct);
        if (!connection.Enabled)
        {
            connection.RecordHealth(
                ConnectionHealthState.Disabled,
                "Connection is disabled.",
                connection.ExternalAccount,
                _clock.UtcNow);
            await _uow.SaveChangesAsync(ct);
            return Map(connection);
        }

        var connector = _connectors.Resolve(connection);
        var health = await connector.TestConnectionAsync(connection, ct);
        connection.RecordHealth(
            health.State,
            health.Message,
            health.ExternalAccount,
            health.CheckedAtUtc);
        await _uow.SaveChangesAsync(ct);
        return Map(connection);
    }

    public async Task<IReadOnlyList<ConnectorResourceDto>> DiscoverAsync(
        Guid connectionId,
        ConnectorDiscoveryQuery query,
        CancellationToken ct)
    {
        var connection = await RequiredConnectionAsync(connectionId, ct);
        if (!connection.Enabled)
            throw new InvalidOperationException("Connection is disabled.");

        var connector = _connectors.Resolve(connection);
        var resources = await connector.DiscoverAsync(connection, query, ct);
        var projects = await _projects.ListAsync(ct);

        return resources.Select(resource =>
        {
            if (string.IsNullOrWhiteSpace(resource.Url))
                return resource;

            var project = projects.FirstOrDefault(candidate =>
                SameRepository(candidate.RepositoryUrl, resource.Url));
            return project is null
                ? resource
                : resource with { DipProjectId = project.Id };
        }).ToArray();
    }

    public async Task<IReadOnlyList<ConnectorCapability>> GetCapabilitiesAsync(
        Guid connectionId,
        CancellationToken ct)
    {
        var connection = await RequiredConnectionAsync(connectionId, ct);
        return await _connectors.Resolve(connection)
            .GetCapabilitiesAsync(connection, ct);
    }

    public async Task<IntegrationConnectionDto> SetEnabledAsync(
        Guid connectionId,
        bool enabled,
        CancellationToken ct)
    {
        var connection = await RequiredConnectionAsync(connectionId, ct);
        connection.SetEnabled(enabled, _clock.UtcNow);
        await _uow.SaveChangesAsync(ct);
        return Map(connection);
    }

    public async Task DeleteAsync(Guid connectionId, CancellationToken ct)
    {
        var connection = await RequiredConnectionAsync(connectionId, ct);
        var reference = connection.CredentialReference;
        _connections.Remove(connection);
        await _uow.SaveChangesAsync(ct);
        await _credentials.DeleteAsync(reference, ct);
    }

    public async Task<ProjectSourceBindingDto> BindProjectAsync(
        CreateProjectSourceBindingCommand command,
        CancellationToken ct)
    {
        if (command.ProjectId == Guid.Empty)
            throw new ArgumentException("ProjectId is required.", nameof(command));
        if (await _projects.GetByIdAsync(command.ProjectId, ct) is null)
            throw new KeyNotFoundException($"Project {command.ProjectId} was not found.");

        var connection = await RequiredConnectionAsync(command.ConnectionId, ct);
        if (!connection.Enabled)
            throw new InvalidOperationException("Cannot bind a disabled connection.");

        var existing = await _bindings.FindAsync(
            command.ProjectId,
            command.ConnectionId,
            command.ExternalResourceId,
            ct);
        if (existing is not null)
            return Map(existing);

        var binding = new ProjectSourceBinding(
            Guid.NewGuid(),
            command.ProjectId,
            command.ConnectionId,
            command.ExternalResourceId,
            command.ExternalResourceName,
            command.ResourceType,
            command.ConfigurationJson,
            _clock.UtcNow);

        await _bindings.AddAsync(binding, ct);
        await _uow.SaveChangesAsync(ct);
        return Map(binding);
    }

    public async Task<IReadOnlyList<ProjectSourceBindingDto>> ListBindingsAsync(
        Guid projectId,
        CancellationToken ct)
        => (await _bindings.ListByProjectAsync(projectId, ct))
            .Select(Map)
            .ToArray();

    public async Task<ProjectSourceSyncSummary> SyncBindingAsync(
        Guid bindingId,
        CancellationToken ct)
    {
        var binding = await _bindings.GetAsync(bindingId, ct)
            ?? throw new KeyNotFoundException(
                $"Project source binding {bindingId} was not found.");

        if (!binding.Enabled)
            throw new InvalidOperationException("Project source binding is disabled.");

        var connection = await RequiredConnectionAsync(binding.ConnectionId, ct);
        if (!connection.Enabled)
            throw new InvalidOperationException("Connection is disabled.");

        var connector = _connectors.Resolve(connection);
        var sync = await connector.SyncAsync(connection, binding, ct);
        var created = 0;
        var existing = 0;

        foreach (var observation in sync.Observations)
        {
            JsonElement? metadata = null;
            if (!string.IsNullOrWhiteSpace(observation.MetadataJson))
            {
                using var document = JsonDocument.Parse(observation.MetadataJson);
                metadata = document.RootElement.Clone();
            }

            var stored = await _evidence.IngestAsync(
                new IngestEvidenceCommand(
                    binding.ProjectId,
                    observation.SourceType,
                    observation.SourceInstanceId,
                    observation.SourceEventId,
                    observation.EventType,
                    observation.SubjectKey,
                    observation.EvidenceKind,
                    observation.Title,
                    observation.Summary,
                    observation.Outcome,
                    observation.Confidence,
                    observation.ObservedAtUtc,
                    SourceReference: observation.SourceReference,
                    ActorId: observation.ActorId,
                    Metadata: metadata),
                ct);

            if (stored.Created) created++;
            else existing++;
        }

        binding.RecordSync(_clock.UtcNow);
        await _uow.SaveChangesAsync(ct);

        return new ProjectSourceSyncSummary(
            binding.Id,
            sync.Observed,
            created,
            existing,
            sync.Warnings,
            binding.LastSyncedAtUtc ?? _clock.UtcNow);
    }

    private async Task<IntegrationConnection> RequiredConnectionAsync(
        Guid id,
        CancellationToken ct)
        => await _connections.GetAsync(id, ct)
           ?? throw new KeyNotFoundException(
               $"Integration connection {id} was not found.");

    private static IntegrationConnectionDto Map(IntegrationConnection item)
        => new(
            item.Id,
            item.Provider,
            item.DisplayName,
            item.Mode,
            item.BaseUrl,
            item.Enabled,
            item.ExternalAccount,
            item.HealthState,
            item.LastHealthMessage,
            item.LastTestedAtUtc,
            item.UpdatedAtUtc);

    private static ProjectSourceBindingDto Map(ProjectSourceBinding item)
        => new(
            item.Id,
            item.ProjectId,
            item.ConnectionId,
            item.ExternalResourceId,
            item.ExternalResourceName,
            item.ResourceType,
            item.Enabled,
            item.LastSyncedAtUtc,
            item.UpdatedAtUtc);

    private static void ValidateCreate(CreateIntegrationConnectionCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!command.Provider.Equals("GitHub", StringComparison.OrdinalIgnoreCase))
            throw new NotSupportedException(
                "Difficulty-1 currently implements GitHub as the first formal connector.");
        if (string.IsNullOrWhiteSpace(command.DisplayName))
            throw new ArgumentException("DisplayName is required.", nameof(command));

        var appMode = command.Mode is
            IntegrationConnectionMode.GitHubApp or
            IntegrationConnectionMode.GitHubEnterpriseApp;
        var patMode = command.Mode is
            IntegrationConnectionMode.GitHubPat or
            IntegrationConnectionMode.GitHubEnterprisePat;

        if (!appMode && !patMode)
            throw new ArgumentException("A supported GitHub connection mode is required.", nameof(command));

        if (patMode && string.IsNullOrWhiteSpace(command.Credential.Token))
            throw new ArgumentException("GitHub PAT is required.", nameof(command));

        if (appMode &&
            (!command.Credential.AppId.HasValue ||
             !command.Credential.InstallationId.HasValue ||
             string.IsNullOrWhiteSpace(command.Credential.PrivateKeyBase64)))
            throw new ArgumentException(
                "GitHub App mode requires AppId, InstallationId and PrivateKeyBase64.",
                nameof(command));
    }

    private static string NormalizeBaseUrl(
        IntegrationConnectionMode mode,
        string? value)
    {
        var enterprise = mode is
            IntegrationConnectionMode.GitHubEnterprisePat or
            IntegrationConnectionMode.GitHubEnterpriseApp;

        if (string.IsNullOrWhiteSpace(value))
        {
            if (enterprise)
                throw new ArgumentException(
                    "GitHub Enterprise mode requires an API base URL.",
                    nameof(value));
            return "https://api.github.com/";
        }

        var normalized = value.Trim();
        if (!Uri.TryCreate(normalized, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("https" or "http"))
            throw new ArgumentException("BaseUrl must be an absolute HTTP(S) URL.", nameof(value));

        return normalized.EndsWith('/') ? normalized
            : normalized + "/";
    }

    private static bool SameRepository(string left, string right)
    {
        static string Normalize(string value)
            => value.Trim().TrimEnd('/').Replace(".git", "", StringComparison.OrdinalIgnoreCase);

        return Normalize(left).Equals(Normalize(right), StringComparison.OrdinalIgnoreCase);
    }
}
