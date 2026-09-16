// Purpose: Defines the IQaGovernanceExecutionCoordinator application boundary.
using Dip.Domain.Enums;
namespace Dip.Application.Features.QualityGovernance;

public interface IQaGovernanceExecutionCoordinator{Task<bool> ExecuteOnceAsync(Guid projectId,Guid qaRunId,string governanceVersion,string inputHash,Func<CancellationToken,Task<string>> action,CancellationToken ct);}
