// Purpose: Defines deterministic asset-intelligence output.
namespace Dip.Application.Features.Assets;

public sealed record AssetIntelligenceResult(
    string AssetKey,
    string State,
    decimal RiskScore,
    bool DuplicateContent,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Actions,
    string Summary);
