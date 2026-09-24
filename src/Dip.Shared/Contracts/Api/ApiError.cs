// Purpose: Implements Api Error responsibilities for api.
namespace Dip.Shared.Contracts.Api; public sealed record ApiError(string Type, string Title, int Status, string TraceId, IReadOnlyDictionary<string, string[]>? Errors = null);
