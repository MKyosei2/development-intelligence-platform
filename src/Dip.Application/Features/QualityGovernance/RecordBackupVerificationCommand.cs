// Purpose: Defines the RecordBackupVerificationCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record RecordBackupVerificationCommand(Guid ProjectId, string BackupKind, string BackupReference, string SourceCommit, string EnvironmentFingerprint, QaBackupVerificationStatus Status, string Detail);
