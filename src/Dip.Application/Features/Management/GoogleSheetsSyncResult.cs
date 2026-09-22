// Purpose: Defines the GoogleSheetsSyncResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record GoogleSheetsSyncResult(int PulledSheets, int PushedSheets, SpreadsheetImportResult? Imported, IReadOnlyList<string> Warnings, DateTimeOffset SyncedAtUtc, int Conflicts = 0, bool DryRun = false);
