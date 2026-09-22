// Purpose: Defines the PageResult application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record PageResult<T>(IReadOnlyList<T> Items, string? NextCursor, int PageSize);
