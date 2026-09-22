// Purpose: Defines the ProjectSourceSyncSummary application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Integrations;

public sealed record ProjectSourceSyncSummary(Guid BindingId, int Observed, int EvidenceCreated, int EvidenceAlreadyPresent, IReadOnlyList<string> Warnings, DateTimeOffset SyncedAtUtc);
