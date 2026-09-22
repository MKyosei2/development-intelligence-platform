// Purpose: Defines the PageRequest application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record PageRequest(string? Cursor = null, int PageSize = 100);
