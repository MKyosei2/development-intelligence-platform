// Purpose: Defines the SmartTestSelectionDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record SmartTestSelectionDto(string TestKey,string CommandJson,int Priority,IReadOnlyList<string> MatchedPaths,string Reason);
