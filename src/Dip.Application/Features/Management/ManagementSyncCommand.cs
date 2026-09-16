// Purpose: Defines the ManagementSyncCommand application contract.
namespace Dip.Application.Features.Management;

public sealed record ManagementSyncCommand(Guid ProjectId,string Provider,ManagementSyncDirection Direction=ManagementSyncDirection.Bidirectional,bool DryRun=false,string DataType="Action",IReadOnlyList<ManagementSyncFieldRule>? FieldRules=null,bool RequireRevisionMatch=true);
