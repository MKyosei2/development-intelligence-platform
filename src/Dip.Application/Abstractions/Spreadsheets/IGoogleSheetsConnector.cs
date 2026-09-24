// Purpose: Implements I Google Sheets Connector responsibilities for spreadsheets.
namespace Dip.Application.Abstractions.Spreadsheets;

public sealed record GoogleSheetGrid(string Name, IReadOnlyList<IReadOnlyList<string>> Rows);

public interface IGoogleSheetsConnector
{
    Task EnsureSheetsAsync(string spreadsheetId, string accessToken, IReadOnlyList<string> sheetNames, CancellationToken ct);
    Task PushAsync(string spreadsheetId, string accessToken, IReadOnlyList<GoogleSheetGrid> sheets, CancellationToken ct);
    Task<IReadOnlyDictionary<string, IReadOnlyList<IReadOnlyList<string>>>> PullAsync(string spreadsheetId, string accessToken, IReadOnlyList<string> sheetNames, CancellationToken ct);
}
