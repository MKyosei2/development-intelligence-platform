// Purpose: Defines Q aG ov er na nc eS ec ur it yE nu ms domain behavior and invariants for i de nt it y.
namespace Dip.Domain.Enums;

public enum ProjectMembershipRole { Viewer, Member, Maintainer, Owner }
public enum QaPolicyLifecycleStatus { Draft, Approved, Active, Superseded, Archived }
public enum QaReleaseOverrideStatus { Requested, Approved, Rejected, Revoked, Expired }
public enum QaProcessedRunStatus { Processing, Completed, Failed }
public enum QaBackupVerificationStatus { Pending, Succeeded, Failed }
