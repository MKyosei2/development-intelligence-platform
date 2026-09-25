// Purpose: Defines QA Governance Security Enums domain values and invariants.
namespace Dip.Domain.Enums;

public enum ProjectMembershipRole { Viewer, Member, Maintainer, Owner }
public enum QaPolicyLifecycleStatus { Draft, Approved, Active, Superseded, Archived }
public enum QaReleaseOverrideStatus { Requested, Approved, Rejected, Revoked, Expired }
public enum QaProcessedRunStatus { Processing, Completed, Failed }
public enum QaBackupVerificationStatus { Pending, Succeeded, Failed }
