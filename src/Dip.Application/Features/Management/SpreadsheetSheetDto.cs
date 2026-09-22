// Purpose: Defines the SpreadsheetSheetDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.Management;

public sealed record SpreadsheetSheetDto(string Name, IReadOnlyList<string> Headers, IReadOnlyList<IReadOnlyList<string>> Rows, bool Editable = false);
