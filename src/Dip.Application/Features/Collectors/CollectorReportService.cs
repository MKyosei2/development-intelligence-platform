using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.Quality;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.Services;
using Dip.Domain.ValueObjects;
using Dip.Shared.Contracts.Collectors;
namespace Dip.Application.Features.Collectors;

public sealed record CollectorReportResult(Guid PipelineRunId,string PipelineStatus,bool WasDuplicate,QualityGateResultDto QualityGate);

public sealed class CollectorReportService
{
    private readonly IProjectRepository _projects; private readonly IPipelineRunRepository _runs;
    private readonly IMetricRepository _metrics; private readonly IQualityIssueRepository _issues;
    private readonly IUnitOfWork _uow; private readonly ISystemClock _clock;
    private readonly QualityGateEvaluator _gate; private readonly CollectorReportValidator _validator;

    public CollectorReportService(IProjectRepository projects,IPipelineRunRepository runs,
        IMetricRepository metrics,IQualityIssueRepository issues,IUnitOfWork uow,ISystemClock clock,
        QualityGateEvaluator gate,CollectorReportValidator validator)
    { _projects=projects;_runs=runs;_metrics=metrics;_issues=issues;_uow=uow;_clock=clock;_gate=gate;_validator=validator; }

    public async Task<CollectorReportResult> ProcessAsync(CollectorReport report,CancellationToken ct)
    {
        _validator.ValidateAndThrow(report);
        var old=await _runs.FindByExternalRunAsync(report.ProjectId,report.ExternalRunId,report.CollectorType,report.AttemptNumber,ct);
        if(old is not null)
            return new(old.Id,old.Status.ToString(),true,new("PreviouslyProcessed",0,0,100,new[]{"Report already processed."}));

        var project=await _projects.GetByIdAsync(report.ProjectId,ct)
            ??throw new KeyNotFoundException($"Project {report.ProjectId} not found.");
        if(!RepositoryMatches(project.RepositoryUrl,report.Repository))
            throw new ArgumentException("Collector repository does not match the project.");

        var run=new PipelineRun(Guid.NewGuid(),project.Id,new CommitSha(report.CommitSha),
            report.ExternalRunId,report.AttemptNumber,report.Branch,report.CollectorType,
            report.CollectorVersion,report.StartedAt);
        run.Start(report.StartedAt); ApplyStatus(run,report.Status,report.FinishedAt);

        var metrics=report.Metrics.Select(x=>new Metric(Guid.NewGuid(),run.Id,x.Name,x.Value,x.Unit,
            x.BaselineValue,null,x.EnvironmentId,x.ScenarioId,x.Platform,x.BuildConfiguration,x.BenchmarkVersion)).ToArray();

        var effective=new List<QualityIssue>();
        foreach(var x in report.Issues)
        {
            var sev=Enum.TryParse<Severity>(x.Severity,true,out var parsed)?parsed:Severity.Info;
            var fp=QualityIssueFingerprint.Create(project.Id,x.Category,x.RuleId,x.Source,x.Title);
            var existing=await _issues.FindOpenByFingerprintAsync(project.Id,fp,ct);
            if(existing is not null){existing.Reoccur(run.Id,sev,_clock.UtcNow);effective.Add(existing);continue;}
            var issue=new QualityIssue(Guid.NewGuid(),project.Id,run.Id,fp,x.Category,sev,x.Title,
                x.Description,x.Source,x.RuleId,x.EvidenceJson,_clock.UtcNow);
            await _issues.AddAsync(issue,ct);effective.Add(issue);
        }

        var gate=_gate.Evaluate(metrics,effective);
        await _runs.AddAsync(run,ct);await _metrics.AddRangeAsync(metrics,ct);
        await _uow.SaveChangesAsync(ct);
        return new(run.Id,run.Status.ToString(),false,gate);
    }

    private static void ApplyStatus(PipelineRun run,CollectorRunStatus s,DateTimeOffset at)
    {
        switch(s)
        {
            case CollectorRunStatus.Succeeded:run.Complete(at);break;
            case CollectorRunStatus.PartialSuccess:run.CompletePartial("Partial success.",at);break;
            case CollectorRunStatus.Failed:run.Fail("Collector failure.",at);break;
            case CollectorRunStatus.Cancelled:run.Cancel("Collector cancelled.",at);break;
            case CollectorRunStatus.TimedOut:run.Timeout("Collector timed out.",at);break;
            default:throw new ArgumentOutOfRangeException(nameof(s));
        }
    }
    private static bool RepositoryMatches(string a,string b)
    {
        static string N(string v)=>v.Trim().TrimEnd('/').Replace(".git","",StringComparison.OrdinalIgnoreCase);
        return N(a).EndsWith(N(b),StringComparison.OrdinalIgnoreCase);
    }
}
