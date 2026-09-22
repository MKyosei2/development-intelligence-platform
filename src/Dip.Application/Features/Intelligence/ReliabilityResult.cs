// Purpose: Defines the ReliabilityResult application contract.
namespace Dip.Application.Features.Intelligence;

public sealed record ReliabilityResult(int Runs, int Passes, int Failures, int Timeouts, decimal FailureRate, decimal FlakyScore, decimal MemoryGrowthMb, decimal PerformanceDriftPercent, string EvidenceJson);
