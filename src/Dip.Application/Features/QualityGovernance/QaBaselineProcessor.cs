// Purpose: Maintains regression baselines from exact-revision QA measurements.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

public sealed class QaBaselineProcessor(IQaGovernanceRepository repository)
{
    public async Task ObserveAsync(
        QaRun run,
        IReadOnlyList<QaFinding> findings,
        DateTimeOffset now,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(run.CommitSha))
            return;

        var value = findings.Count(x => x.Severity is Severity.Critical or Severity.Warning);
        var healthy = run.Status == QaRunStatus.Succeeded && value == 0;

        foreach (var metric in new[]
                 {
                     ("qa.finding_count", (decimal)value),
                     ("qa.critical_count", (decimal)findings.Count(x => x.Severity == Severity.Critical))
                 })
        {
            var baseline = await repository.FindBaselineAsync(
                run.ProjectId, run.SubjectKey, metric.Item1, ct);
            if (baseline is null)
            {
                if (healthy)
                {
                    baseline = new QaRegressionBaseline(
                        Guid.NewGuid(),
                        run.ProjectId,
                        run.SubjectKey,
                        metric.Item1,
                        run.CommitSha!,
                        metric.Item2,
                        now);
                    await repository.AddBaselineAsync(baseline, ct);
                }
                continue;
            }

            baseline.Observe(run.CommitSha!, metric.Item2, healthy, now);
        }
    }
}
