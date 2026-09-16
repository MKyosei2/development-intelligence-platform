// Purpose: Defines the ManagementWorkbookDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record ManagementWorkbookDto(Guid ProjectId,IReadOnlyList<SpreadsheetSheetDto> Sheets,DateTimeOffset GeneratedAtUtc);
