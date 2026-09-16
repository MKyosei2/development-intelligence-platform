// Purpose: Defines the RebaselineQaCommand application contract.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public sealed record RebaselineQaCommand(Guid ProjectId,string SubjectKey,string MetricKey,string CommitSha,decimal Value);
