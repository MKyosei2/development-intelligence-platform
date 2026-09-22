// Purpose: Owns QA finding lifecycle, waiver expiry, and ownership assignment for a completed run.
using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.QualityGovernance;

public sealed record QaFindingLifecycleResult(IReadOnlyList<QaWaiver> ActiveWaivers);

public sealed class QaFindingLifecycleProcessor(
    IQaGovernanceRepository repository,
    IUnitOfWork unitOfWork,
    IRepositoryOwnershipResolver ownershipResolver,
    IQaFindingRepository findings)
{
    public async Task<QaFindingLifecycleResult> ProcessAsync(
        QaRun run,
        IReadOnlyList<QaFinding> runFindings,
        DateTimeOffset now,
        CancellationToken ct)
    {
        var waivers = (await repository.ListWaiversAsync(run.ProjectId, ct)).ToList();
        foreach (var waiver in waivers)
            waiver.Expire(now);

        var activeWaivers = waivers.Where(x => x.IsActive(now)).ToArray();
        var ownershipRules = await repository.ListOwnershipRulesAsync(run.ProjectId, ct);
        var observed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var finding in runFindings)
        {
            observed.Add(finding.Fingerprint);
            var state = await repository.FindFindingStateAsync(run.ProjectId, finding.Fingerprint, ct);
            if (state is null)
            {
                state = new QaFindingState(
                    Guid.NewGuid(), run.ProjectId, finding.Fingerprint, finding.Id,
                    run.SubjectKey, run.CommitSha, now);
                await repository.AddFindingStateAsync(state, ct);
            }
            else
            {
                state.Observe(finding.Id, run.CommitSha, now);
            }

            var waiver = activeWaivers.FirstOrDefault(
                x => QaGovernanceMatching.MatchesWaiver(x, finding));
            if (waiver is not null)
            {
                state.Transition(
                    FindingLifecycleStatus.Suppressed,
                    $"Active waiver: {waiver.Name}; expires {waiver.ExpiresAtUtc:O}",
                    waiver.ApprovedBy ?? waiver.RequestedBy,
                    now);
            }
            else if (state.Status == FindingLifecycleStatus.Suppressed)
            {
                state.Transition(
                    FindingLifecycleStatus.Confirmed,
                    "The previous waiver no longer applies.",
                    "DIP",
                    now);
            }

            if (waiver is null
                && QaGovernanceMatching.TryOwner(ownershipRules, finding.FilePath, out var owner))
            {
                state.Assign(owner!.Team, owner.Workstream, QaOwnershipSource.Rule, now);
            }
            else if (waiver is null)
            {
                var resolved = await ownershipResolver.ResolveAsync(
                    run.WorkspaceRelativePath, finding.FilePath, ct);
                if (resolved is not null)
                {
                    state.Assign(
                        resolved.Team,
                        resolved.Workstream,
                        QaOwnershipSource.CodeOwners,
                        now);
                }
            }
        }

        await unitOfWork.SaveChangesAsync(ct);

        if (run.Status != QaRunStatus.InfrastructureFailure)
        {
            var states = await repository.ListFindingStatesAsync(run.ProjectId, 10000, ct);
            foreach (var state in states.Where(x =>
                         string.Equals(x.SubjectKey, run.SubjectKey, StringComparison.OrdinalIgnoreCase)
                         && !observed.Contains(x.Fingerprint)))
            {
                if (state.Status is FindingLifecycleStatus.New
                    or FindingLifecycleStatus.Confirmed
                    or FindingLifecycleStatus.Assigned
                    or FindingLifecycleStatus.InProgress)
                {
                    state.Transition(
                        FindingLifecycleStatus.Fixed,
                        "Not observed in the latest completed QA run.",
                        "DIP",
                        now);
                }
                else if (state.Status == FindingLifecycleStatus.Fixed)
                {
                    state.Transition(
                        FindingLifecycleStatus.Verified,
                        "Absent from two consecutive completed QA runs.",
                        "DIP",
                        now);
                }
                else if (state.Status == FindingLifecycleStatus.Suppressed)
                {
                    var last = await findings.GetAsync(state.LastFindingId, ct);
                    if (last is not null
                        && !activeWaivers.Any(x => QaGovernanceMatching.MatchesWaiver(x, last)))
                    {
                        state.Transition(
                            FindingLifecycleStatus.Fixed,
                            "The waiver no longer applies and the finding was not observed in the latest completed QA run.",
                            "DIP",
                            now);
                    }
                }
            }
        }

        await unitOfWork.SaveChangesAsync(ct);
        return new(activeWaivers);
    }
}
