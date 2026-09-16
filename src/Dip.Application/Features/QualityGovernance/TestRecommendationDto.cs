// Purpose: Defines the TestRecommendationDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record TestRecommendationDto(string TestKey,string Reason,string Priority,string? CommandJson);
