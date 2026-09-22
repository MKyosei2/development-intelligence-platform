// Purpose: Defines QaPolicyRules.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaPolicyRules(int MaxCriticalFindings = 0, int MaxWarningFindings = 50, decimal MaxFlakyRatePercent = 20m, int MaxInfrastructureFailures = 0, int MaxEvidenceAgeDays = 14, long MaxRunDurationMs = 900000, bool BlockOnStaleEvidence = true, bool BlockOnUnavailableQa = true, decimal MaxCpuSeconds = 600m, long MaxArtifactBytes = 2147483648L);
