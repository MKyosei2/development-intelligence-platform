// Purpose: Defines the ReproductionAttemptResult application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record ReproductionAttemptResult(int AttemptNumber, ReproductionStatus Status, bool Reproduced, bool Crashed, bool TimedOut, int? ExitCode, long DurationMs, string ArtifactDirectory, string? VideoPath, string? AudioPath, string? DumpPath, string ResultJson);
