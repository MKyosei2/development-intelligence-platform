// Purpose: Defines the ReproductionResult application contract.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
namespace Dip.Application.Features.QualityAutomation;

public sealed record ReproductionResult(ReproductionStatus Status,int ReproducedAttempts,int TotalAttempts,IReadOnlyList<ReproductionAttemptResult> Attempts);
