// Purpose: Defines the ProjectSourceSyncResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.ProjectIntelligence;

public sealed record ProjectSourceSyncResult(Guid ProjectId, string Provider, int Observed, int AddedOrChanged, int Unchanged, int EvidenceRecords, int AutoResolved, bool DryRun, IReadOnlyList<string> Warnings, DateTimeOffset SyncedAtUtc);
