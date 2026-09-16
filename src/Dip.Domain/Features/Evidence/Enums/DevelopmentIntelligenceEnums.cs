// Purpose: Defines D ev el op me nt In te ll ig en ce En um s states used by Evidence.
namespace Dip.Domain.Enums;

public enum GameReplayRunStatus { Queued, Running, Succeeded, Failed, Cancelled, InfrastructureFailure }
public enum PerformanceBudgetVerdict { NotEvaluated, Passed, Warning, Failed }
public enum ChangeRiskLevel { Low, Medium, High, Critical }
public enum ReleaseDecision { Go, ConditionalGo, NoGo }
public enum ExternalWorkItemProvider { GitHub, Jira, Linear }
public enum ExternalWorkItemStatus { Preview, Open, Closed }
public enum ReleaseEvidenceBundleStatus { Preparing, Succeeded, Failed }
public enum RepositoryDependencyKind { Build, Runtime, Schema, Tooling, Asset, Service, Test }
public enum CrossRepositoryImpactLevel { None, Low, Medium, High, Critical }
