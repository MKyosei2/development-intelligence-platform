// Purpose: Defines the ManagementSyncResult application contract.
namespace Dip.Application.Features.Management;

public sealed record ManagementSyncResult(Guid ProjectId,string Provider,int Pulled,int Pushed,int Conflicts,bool DryRun,IReadOnlyList<ManagementSyncChange> Changes,DateTimeOffset SyncedAtUtc);
