// Purpose: Defines the MediaRegressionResult application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record MediaRegressionResult(bool Passed,decimal DifferencePercent,string Kind,string DiffArtifact,string EvidenceJson);
