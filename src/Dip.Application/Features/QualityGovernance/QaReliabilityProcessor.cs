// Purpose: Updates suite and test reliability observations from completed QA findings.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

public sealed class QaReliabilityProcessor(
    IQaGovernanceRepository repository,
    IUnitOfWork unitOfWork)
{
    public async Task ProcessAsync(
        QaRun run,
        IReadOnlyList<QaFinding> findings,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var suiteKey = $"suite:{run.SubjectKey}";
        var suite = await repository.FindReliabilityAsync(run.ProjectId, suiteKey, ct);
        if (suite is null)
        {
            suite = new QaTestReliability(Guid.NewGuid(), run.ProjectId, suiteKey, now);
            await repository.AddReliabilityAsync(suite, ct);
        }

        var infrastructureFailure = run.Status == QaRunStatus.InfrastructureFailure;
        var suitePassed = !infrastructureFailure
            && !findings.Any(QaGovernanceMatching.IsProductFailure);
        suite.Observe(suitePassed, infrastructureFailure, run.CommitSha, now);

        foreach (var finding in findings.Where(
                     x => x.Category.Contains("test", StringComparison.OrdinalIgnoreCase)))
        {
            var key = $"test:{finding.Title}";
            var reliability = await repository.FindReliabilityAsync(run.ProjectId, key, ct);
            if (reliability is null)
            {
                reliability = new QaTestReliability(Guid.NewGuid(), run.ProjectId, key, now);
                await repository.AddReliabilityAsync(reliability, ct);
            }

            if (finding.TotalAttempts >= 3
                && finding.ReproductionStatus is ReproductionStatus.Reproduced or ReproductionStatus.NotReproduced)
            {
                for (var i = 0; i < finding.ReproducedAttempts; i++)
                    reliability.Observe(false, false, run.CommitSha, now);

                for (var i = finding.ReproducedAttempts; i < finding.TotalAttempts; i++)
                    reliability.Observe(true, false, run.CommitSha, now);
            }
            else
            {
                reliability.Observe(false, false, run.CommitSha, now);
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
    }
}
