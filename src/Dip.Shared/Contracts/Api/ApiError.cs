// Purpose: Defines a focused responsibility within the development intelligence platform.
namespace Dip.Shared.Contracts.Api; public sealed record ApiError(string Type, string Title, int Status, string TraceId, IReadOnlyDictionary<string, string[]>? Errors = null);
