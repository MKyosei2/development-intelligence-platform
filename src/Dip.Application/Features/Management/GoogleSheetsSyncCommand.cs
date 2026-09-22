// Purpose: Defines the GoogleSheetsSyncCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record GoogleSheetsSyncCommand(Guid ProjectId, string SpreadsheetId, string AccessToken, bool PullEditableSheets = true, bool PushCalculatedSheets = true, bool DryRun = false);
