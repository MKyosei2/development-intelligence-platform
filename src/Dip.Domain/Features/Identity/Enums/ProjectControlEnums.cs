// Purpose: Defines P ro je ct Co nt ro lE nu ms states used by Identity.
namespace Dip.Domain.Enums;

public enum DevelopmentGraphNodeType
{
    Repository, Branch, Commit, PullRequest, Review, ChangeSet, File, Component,
    Build, CiRun, Test, QaFinding, Incident, WorkItem, Feature, AcceptanceCriterion,
    Asset, Team, Workstream, Task, Blocker, Requirement, ScopeBaseline, Milestone, Release, Deployment, Evidence,
    Risk, ManagementDecision, ActionItem, ChangeRequest, ResourceAllocation,
    ResponsibilityAssignment, ManagementRoleBinding, WorkCalendar, ApprovalRequest, AlertState, SourceAuthority, ExternalSyncState,
    ProjectPlanImport, PlanItem, ExternalObservation, EntityBinding, ManagementDigest
}

public enum DevelopmentGraphEdgeType
{
    Contains, BelongsTo, Changes, DependsOn, Affects, Impacts, TestedBy, VerifiedBy,
    BuiltBy, Blocks, Targets, Reviews, Produces, RelatesTo, Requires, Implements,
    EvidenceFor, Deploys, RegressedBy, CriticalPath, AssignedTo, OwnedBy, ScheduledFor, Satisfies,
    Mitigates, ResultsIn, AllocatesTo, TriggeredBy, Supersedes,
    ResponsibleFor, AccountableFor, ConsultedOn, InformedOn, Approves, SyncedWith, EquivalentTo, ObservedBy, DerivedFrom, Contradicts
}

public enum VersionControlProvider { GitHub, Perforce }
public enum ExternalCiProvider { GitHubActions, Jenkins, Generic }
public enum ProjectControlRiskLevel { Low, Medium, High, Critical }
public enum ReadinessState { Unknown, NotReady, NeedsAttention, Ready, Blocked }
public enum MilestoneState { Planned, Active, AtRisk, Blocked, Completed, Cancelled }
