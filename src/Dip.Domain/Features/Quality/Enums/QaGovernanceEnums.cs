// Purpose: Defines QA Governance Enums domain behavior and invariants for quality.
namespace Dip.Domain.Enums;

public enum FindingLifecycleStatus { New, Confirmed, Assigned, InProgress, Fixed, Verified, Closed, Suppressed, AcceptedRisk, Duplicate, WontFix }
public enum WaiverStatus { Requested, Approved, Active, Rejected, Expired, Revoked }
public enum TestReliabilityClassification { Unknown, StablePass, StableFail, Flaky, EnvironmentFailure }
public enum QaGateVerdict { Go, ReviewRequired, NoGo, Unavailable }
public enum QaSystemHealthStatus { Healthy, Degraded, Unavailable }
public enum QaRegressionStatus { Baseline, Healthy, Regressed, Recovered }
public enum QaOwnershipSource { Rule, CodeOwners, Manual, Unassigned }
public enum QaFreshnessStatus { Fresh, Aging, Stale, Unknown }
public enum QaMinimizationStatus { Queued, Running, Succeeded, NoReduction, InfrastructureFailure, Cancelled }
