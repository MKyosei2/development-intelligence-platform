// Purpose: Orchestrates the DIP workflow for E vi de nc eI ng es ti on Se rv ic e.
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

    public EvidenceIngestionService(
        IProjectRepository projects,
        IEvidenceV2Repository evidence,
        IUnitOfWork uow,
        ISystemClock clock,
        PolicyResolver policies)
    {
        _projects = projects;
        _evidence = evidence;
        _uow = uow;
        _clock = clock;
        _policies = policies;
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

        var effective = await _policies.ResolveAsync(
            command.ProjectId,
            agentId: null,
            ct);

        var metadataJson = effective.Evidence.CollectMetadata
            ? command.Metadata?.GetRawText()
            : null;

        // Hash-only means provider/raw payload is not retained. Normalized metadata
        // may still be kept when CollectMetadata is allowed.
        var payloadJson = effective.Privacy.HashOnly
            ? null
            : command.Payload?.GetRawText();

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
            effective.Evidence.CalculateContentHash
                ? command.ContentHash
                : null,
            command.ParentEvidenceId,
            command.PrivacyClass,
            EvidenceIntegrityStatus.Unverified);

        await _evidence.AddAsync(item, ct);

        try
        {
            await _uow.SaveChangesAsync(ct);
            return Map(item, created: true);
        }
        catch
        {
            // A concurrent retry may win the unique source-event index. Re-query so
            // callers still get idempotent behavior instead of a duplicate failure.
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

    private static IngestEvidenceResult Map(
        EvidenceRecord item,
        bool created)
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
