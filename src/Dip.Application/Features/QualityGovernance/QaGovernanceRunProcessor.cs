using System.Security.Cryptography;
using System.Text;
using Dip.Domain.Entities;

namespace Dip.Application.Features.QualityGovernance;

/// <summary>Owns exactly-once orchestration for completed QA runs. The database coordinator serializes duplicate workers and wraps all governance writes in one transaction.</summary>
public sealed class QaGovernanceRunProcessor(QaGovernanceCoreEngine core,IQaGovernanceExecutionCoordinator coordinator)
{
    public const string GovernanceVersion="2.0.0";
    public Task<bool> ProcessCompletedRunAsync(QaRun run,IReadOnlyList<QaFinding> findings,CancellationToken ct)
    {
        var inputHash=Hash($"{run.Id}|{run.ProjectId}|{run.CommitSha}|{string.Join('|',findings.OrderBy(x=>x.Fingerprint).Select(x=>$"{x.Fingerprint}:{x.Severity}:{x.ReproductionStatus}:{x.TotalAttempts}:{x.ReproducedAttempts}"))}");
        return coordinator.ExecuteOnceAsync(run.ProjectId,run.Id,GovernanceVersion,inputHash,async inner=>
        {
            await core.ProcessCompletedRunAsync(run,findings,inner);
            return Hash($"{run.Id}|{GovernanceVersion}|{findings.Count}|{inputHash}");
        },ct);
    }
    private static string Hash(string value)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
