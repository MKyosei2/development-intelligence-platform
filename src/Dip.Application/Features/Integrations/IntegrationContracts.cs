using Dip.Domain.Enums;

namespace Dip.Application.Features.Integrations;

public sealed record ConnectorCapability(
    string Key,
    bool Supported,
    string? Reason = null);

public sealed record ConnectorHealth(
    ConnectionHealthState State,
    string Message,
    DateTimeOffset CheckedAtUtc,
    string? ExternalAccount,
    IReadOnlyList<ConnectorCapability> Capabilities);

public sealed record ConnectorDiscoveryQuery(
    string? Search = null,
    string? Visibility = null,
    int Limit = 500);

public sealed record ConnectorResourceDto(
    string ExternalId,
    string ResourceType,
    string Name,
    string FullName,
    string? Url,
    string? DefaultBranch,
    bool Private,
    bool Archived,
    string? PrimaryLanguage,
    string? Description,
    DateTimeOffset? UpdatedAtUtc,
    Guid? DipProjectId = null);

public sealed record ConnectorSyncObservation(
    EvidenceSourceType SourceType,
    string SourceInstanceId,
    string SourceEventId,
    EvidenceEventType EventType,
    string SubjectKey,
    string EvidenceKind,
    string Title,
    string Summary,
    EvidenceOutcome Outcome,
    EvidenceConfidence Confidence,
    DateTimeOffset ObservedAtUtc,
    string? SourceReference = null,
    string? ActorId = null,
    string? MetadataJson = null);

public sealed record ConnectorSyncResult(
    int Observed,
    IReadOnlyList<ConnectorSyncObservation> Observations,
    IReadOnlyList<string> Warnings);

public sealed record IntegrationCredentialInput(
    string? Token = null,
    long? AppId = null,
    long? InstallationId = null,
    string? AppSlug = null,
    string? PrivateKeyBase64 = null);

public sealed record CreateIntegrationConnectionCommand(
    string Provider,
    string DisplayName,
    IntegrationConnectionMode Mode,
    string? BaseUrl,
    IntegrationCredentialInput Credential,
    bool TestAfterSave = true);

public sealed record IntegrationConnectionDto(
    Guid Id,
    string Provider,
    string DisplayName,
    IntegrationConnectionMode Mode,
    string? BaseUrl,
    bool Enabled,
    string? ExternalAccount,
    ConnectionHealthState HealthState,
    string? LastHealthMessage,
    DateTimeOffset? LastTestedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record CreateProjectSourceBindingCommand(
    Guid ProjectId,
    Guid ConnectionId,
    string ExternalResourceId,
    string ExternalResourceName,
    string ResourceType,
    string? ConfigurationJson = null);

public sealed record ProjectSourceBindingDto(
    Guid Id,
    Guid ProjectId,
    Guid ConnectionId,
    string ExternalResourceId,
    string ExternalResourceName,
    string ResourceType,
    bool Enabled,
    DateTimeOffset? LastSyncedAtUtc,
    DateTimeOffset UpdatedAtUtc);

public sealed record ProjectSourceSyncSummary(
    Guid BindingId,
    int Observed,
    int EvidenceCreated,
    int EvidenceAlreadyPresent,
    IReadOnlyList<string> Warnings,
    DateTimeOffset SyncedAtUtc);


public sealed record ImportConnectionProjectCommand(
    string ExternalResourceId,
    string ExternalResourceName,
    string ProjectName,
    DateTimeOffset? ReleaseDateUtc = null,
    bool SyncEvidence = true);

public sealed record ImportConnectionProjectResult(
    Guid ProjectId,
    string Name,
    string RepositoryUrl,
    string DefaultBranch,
    Guid BindingId,
    int EvidenceCreated,
    int EvidenceAlreadyPresent,
    string? Warning = null);
