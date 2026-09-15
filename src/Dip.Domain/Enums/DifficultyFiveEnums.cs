namespace Dip.Domain.Enums;

public enum AgentFleetStatus { Pending, Active, Offline, Revoked, UpgradeRequired }
public enum AgentReleaseChannel { Stable, Preview, Canary }
public enum AgentRolloutState { Draft, Active, Paused, Completed, RolledBack }
public enum EvidenceChainStatus { Verified, Unsigned, InvalidSignature, BrokenChain }
public enum DistributedIngestionState { Pending, Processing, Completed, Failed, DeadLetter }
public enum OrganizationRole { OrganizationAdmin, Producer, ProjectManager, Lead, Developer, Artist, Qa, Viewer, Agent }
public enum IdentityKind { Person, ExternalAccount, Machine, ServiceIdentity }
public enum RetentionAction { Keep, ExpireMetadata, DeletePayload, LegalHold }
