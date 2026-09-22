// Purpose: Defines the ManagementSyncFieldRule application contract.
namespace Dip.Application.Features.Management;

public sealed record ManagementSyncFieldRule(string Field, string Authority = "Manual", string ConflictPolicy = "Manual");
