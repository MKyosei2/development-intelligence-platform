// Purpose: Ingests project evidence while preserving provenance, policy context, and cryptographically verified integrity state.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Policies;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Evidence;

public sealed class EvidenceIngestionService
{
    private readonly IProjectRepository _projects;
    private readonly IEvidenceV2Repository _evidence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly PolicyResolver _policies;
    private readonly IArtifactRepository? _artifacts;
    private readonly EvidenceIntegrityVerifier? _integrityVerifier;

    public EvidenceIngestionService(
        IProjectRepository projects,
        IEvidenceV2Repository evidence,
        IUnitOfWork uow,
        ISystemClock clock,
        PolicyResolver policies,
        IArtifactRepository? artifacts = null,
        EvidenceIntegrityVerifier? integrityVerifier = null)
    {
        _projects = projects;
        _evidence = evidence;
        _uow = uow;
        _clock = clock;
        _policies = policies;
        _artifacts = artifacts;
        _integrityVerifier = integrityVerifier;
    }

    public async Task<IngestEvidenceResult> IngestAsync(
        IngestEvidenceCommand command,
        CancellationToken ct)
    {
        var now = _clock.UtcNow;
        EvidenceValidator.Validate(command, now);

        if (await _projects.GetByIdAsync(command.ProjectId, ct) is null)
            throw new KeyNotFoundException($"Project {command.ProjectId} was not found.");

        var existing = await _evidence.FindBySourceEventAsync(
            command.ProjectId,
            command.SourceType,
            command.SourceInstanceId.Trim(),
            command.SourceEventId.Trim(),
            ct);
        if (existing is not null)
            return Map(existing, created: false);

        var effective = await _policies.ResolveAsync(command.ProjectId, agentId: null, ct);
        var metadataJson = effective.Evidence.CollectMetadata
            ? command.Metadata?.GetRawText()
            : null;
        var payloadJson = effective.Privacy.HashOnly
            ? null
            : command.Payload?.GetRawText();
        var retainedContentHash = effective.Evidence.CalculateContentHash
            ? command.ContentHash
            : null;
        var integrityStatus = await DetermineIntegrityStatusAsync(command, retainedContentHash, ct);

        var item = EvidenceRecord.CreateV2(
            Guid.NewGuid(),
            command.ProjectId,
            command.PipelineRunId,
            command.SubjectKey,
            command.SourceType,
            command.SourceInstanceId,
            command.SourceEventId,
            command.EventType,
            command.EvidenceKind,
            command.Outcome,
            command.Title,
            command.Summary,
            command.Confidence,
            command.SourceReference,
            metadataJson,
            payloadJson,
            command.ObservedAtUtc,
            now,
            command.ActorId,
            command.MachineId,
            command.ArtifactId,
            retainedContentHash,
            command.ParentEvidenceId,
            command.PrivacyClass,
            integrityStatus);

        await _evidence.AddAsync(item, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
            return Map(item, created: true);
        }
        catch
        {
            var raced = await _evidence.FindBySourceEventAsync(
                command.ProjectId,
                command.SourceType,
                command.SourceInstanceId.Trim(),
                command.SourceEventId.Trim(),
                ct);
            if (raced is not null)
                return Map(raced, created: false);
            throw;
        }
    }

    private async Task<EvidenceIntegrityStatus> DetermineIntegrityStatusAsync(
        IngestEvidenceCommand command,
        string? retainedContentHash,
        CancellationToken ct)
    {
        if (command.SourceType == EvidenceSourceType.HumanReport)
            return EvidenceIntegrityStatus.Unverified;

        if (_integrityVerifier is not null && ShouldUseCryptographicBoundary(command.Metadata))
        {
            Guid? artifactRecordId = Guid.TryParse(command.ArtifactId, out var parsedArtifact)
                ? parsedArtifact
                : null;
            var result = await _integrityVerifier.VerifyAsync(
                new EvidenceIntegrityVerificationRequest(
                    command.ProjectId,
                    command.SourceType,
                    command.SourceInstanceId,
                    command.SourceEventId,
                    command.EventType,
                    command.SubjectKey,
                    command.ObservedAtUtc,
                    artifactRecordId,
                    retainedContentHash,
                    command.ParentEvidenceId,
                    Metadata(command.Metadata, "signingKeyId"),
                    Metadata(command.Metadata, "signatureAlgorithm"),
                    Metadata(command.Metadata, "signatureBase64") ?? Metadata(command.Metadata, "signature"),
                    Metadata(command.Metadata, "signedPayloadBase64")),
                ct);
            return result.Status;
        }

        // Compatibility path for installations that have not enabled source trust
        // policy yet. A caller-supplied hash alone is still never enough: DIP must
        // resolve the immutable artifact or an already-verified parent.
        if (command.ParentEvidenceId is Guid parentId)
        {
            var parent = await _evidence.GetAsync(command.ProjectId, parentId, ct);
            if (parent is not null
                && parent.ProjectId == command.ProjectId
                && parent.IntegrityStatus == EvidenceIntegrityStatus.Verified)
                return EvidenceIntegrityStatus.Verified;
        }

        if (_artifacts is null
            || string.IsNullOrWhiteSpace(command.ArtifactId)
            || !Guid.TryParse(command.ArtifactId, out var artifactId))
            return EvidenceIntegrityStatus.Unverified;

        var record = await _artifacts.GetRecordAsync(artifactId, ct);
        if (record is null || record.ProjectId != command.ProjectId)
            return EvidenceIntegrityStatus.Unverified;
        var blob = await _artifacts.GetBlobAsync(record.ArtifactBlobId, ct);
        if (blob is null)
            return EvidenceIntegrityStatus.Unverified;
        if (!string.IsNullOrWhiteSpace(retainedContentHash)
            && !string.Equals(retainedContentHash.Trim(), blob.Sha256, StringComparison.OrdinalIgnoreCase))
            return EvidenceIntegrityStatus.Invalid;
        return EvidenceIntegrityStatus.Verified;
    }

    private static bool ShouldUseCryptographicBoundary(JsonElement? metadata)
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DIP_EVIDENCE_SOURCE_POLICY_JSON"))
           || !string.IsNullOrWhiteSpace(Metadata(metadata, "signatureBase64"))
           || !string.IsNullOrWhiteSpace(Metadata(metadata, "signature"));

    private static string? Metadata(JsonElement? metadata, string name)
    {
        if (!metadata.HasValue || metadata.Value.ValueKind != JsonValueKind.Object)
            return null;
        foreach (var property in metadata.Value.EnumerateObject())
        {
            if (property.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                return property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString()
                    : property.Value.GetRawText();
        }
        return null;
    }

    private static IngestEvidenceResult Map(EvidenceRecord item, bool created)
        => new(
            item.Id,
            created,
            item.ProjectId,
            item.SourceType,
            item.SourceInstanceId ?? string.Empty,
            item.SourceEventId ?? string.Empty,
            item.EventType,
            item.ObservedAtUtc,
            item.CreatedAtUtc);
}
