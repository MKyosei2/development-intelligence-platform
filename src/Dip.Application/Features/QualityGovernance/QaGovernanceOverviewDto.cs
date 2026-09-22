// Purpose: Defines the QaGovernanceOverviewDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaGovernanceOverviewDto(Guid ProjectId, string GateVerdict, string GateSummary, int OpenFindings, int ActiveWaivers, int FlakyTests, int RegressedBaselines, string SystemHealth, decimal TechnicalDebtScore, string Freshness, DateTimeOffset GeneratedAtUtc, string TrendDirection = "Unknown", decimal TechnicalDebtDelta = 0m, string EvidenceConfidence = "Unverified", decimal EvidenceConfidencePercent = 0m);
