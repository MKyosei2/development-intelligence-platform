// Purpose: Defines the QaBackupVerificationDto application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record QaBackupVerificationDto(Guid Id,Guid ProjectId,string BackupKind,string BackupReference,string SourceCommit,string EnvironmentFingerprint,QaBackupVerificationStatus Status,string Detail,DateTimeOffset VerifiedAtUtc);
