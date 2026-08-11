using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Performance;

public sealed record PerformanceBudgetRule(string Metric,string Aggregation,decimal? Minimum,decimal? Maximum,string Unit,decimal WarningPercent=5m);
public sealed record CreatePerformanceBudgetCommand(Guid? ProjectId,string Key,string Name,string Platform,string TargetProfile,IReadOnlyList<PerformanceBudgetRule> Rules,bool Enabled=true);
public sealed record PerformanceBudgetProfileDto(Guid Id,Guid? ProjectId,string Key,string Name,string Platform,string TargetProfile,bool Enabled,IReadOnlyList<PerformanceBudgetRule> Rules);
public sealed record PerformanceMetricInput(string Name,string Aggregation,decimal Value,string Unit);
public sealed record PerformanceBudgetViolation(string Metric,string Aggregation,decimal Value,string Unit,decimal? Minimum,decimal? Maximum,string Severity,string Message);
public sealed record PerformanceBudgetEvaluationDto(Guid Id,Guid ProfileId,string Verdict,string Summary,IReadOnlyList<PerformanceBudgetViolation> Violations,DateTimeOffset EvaluatedAtUtc);

public sealed class PerformanceBudgetService
{
    private readonly IPerformanceBudgetRepository _repo;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public PerformanceBudgetService(IPerformanceBudgetRepository repo,IUnitOfWork uow,ISystemClock clock){_repo=repo;_uow=uow;_clock=clock;}

    public async Task<PerformanceBudgetProfileDto> CreateAsync(CreatePerformanceBudgetCommand command,CancellationToken ct)
    {
        if(command.Rules.Count==0)throw new ArgumentException("At least one performance budget rule is required.",nameof(command));
        if(await _repo.GetProfileByKeyAsync(command.ProjectId,command.Key,ct) is not null)throw new InvalidOperationException($"Performance budget '{command.Key}' already exists for this scope.");
        var normalized=command.Rules.Select(ValidateRule).ToArray();var entity=new PerformanceBudgetProfile(Guid.NewGuid(),command.ProjectId,command.Key,command.Name,command.Platform,command.TargetProfile,JsonSerializer.Serialize(normalized),command.Enabled,_clock.UtcNow);await _repo.AddProfileAsync(entity,ct);await _uow.SaveChangesAsync(ct);return Map(entity);
    }
    public async Task<IReadOnlyList<PerformanceBudgetProfileDto>> ListAsync(Guid? projectId,CancellationToken ct)=>(await _repo.ListProfilesAsync(projectId,ct)).Select(Map).ToArray();
    public async Task<PerformanceBudgetEvaluationDto> EvaluateAsync(Guid projectId,Guid profileId,IReadOnlyCollection<PerformanceMetricInput> metrics,Guid? replayRunId,Guid? benchmarkRunId,CancellationToken ct)
    {
        var profile=await _repo.GetProfileAsync(profileId,ct)??throw new KeyNotFoundException($"Performance budget profile {profileId} was not found.");
        if(profile.ProjectId.HasValue&&profile.ProjectId.Value!=projectId)throw new InvalidOperationException("The selected performance budget belongs to another project.");
        if(!profile.Enabled)throw new InvalidOperationException("Performance budget profile is disabled.");
        var rules=JsonSerializer.Deserialize<PerformanceBudgetRule[]>(profile.RulesJson)??Array.Empty<PerformanceBudgetRule>();var violations=new List<PerformanceBudgetViolation>();var warning=false;var failed=false;
        foreach(var rule in rules)
        {
            var metric=metrics.FirstOrDefault(x=>x.Name.Equals(rule.Metric,StringComparison.OrdinalIgnoreCase)&&x.Aggregation.Equals(rule.Aggregation,StringComparison.OrdinalIgnoreCase));
            if(metric is null){warning=true;violations.Add(new(rule.Metric,rule.Aggregation,0m,rule.Unit,rule.Minimum,rule.Maximum,"Warning",$"Metric {rule.Metric}/{rule.Aggregation} was not reported."));continue;}
            var hardFail=(rule.Minimum.HasValue&&metric.Value<rule.Minimum.Value)||(rule.Maximum.HasValue&&metric.Value>rule.Maximum.Value);
            if(hardFail){failed=true;violations.Add(new(rule.Metric,rule.Aggregation,metric.Value,metric.Unit,rule.Minimum,rule.Maximum,"Failed",$"{metric.Value:0.###} {metric.Unit} is outside the allowed range."));continue;}
            if(rule.WarningPercent>0)
            {
                var nearMin=rule.Minimum.HasValue&&metric.Value<rule.Minimum.Value*(1m+rule.WarningPercent/100m);var nearMax=rule.Maximum.HasValue&&metric.Value>rule.Maximum.Value*(1m-rule.WarningPercent/100m);
                if(nearMin||nearMax){warning=true;violations.Add(new(rule.Metric,rule.Aggregation,metric.Value,metric.Unit,rule.Minimum,rule.Maximum,"Warning",$"{metric.Value:0.###} {metric.Unit} is within {rule.WarningPercent:0.#}% of the budget boundary."));}
            }
        }
        var verdict=failed?PerformanceBudgetVerdict.Failed:warning?PerformanceBudgetVerdict.Warning:PerformanceBudgetVerdict.Passed;var summary=$"Performance budget {profile.Key}: {verdict}; {violations.Count} attention item(s).";var entity=new PerformanceBudgetEvaluation(Guid.NewGuid(),projectId,profile.Id,replayRunId,benchmarkRunId,verdict,summary,JsonSerializer.Serialize(violations),_clock.UtcNow);await _repo.AddEvaluationAsync(entity,ct);await _uow.SaveChangesAsync(ct);return new(entity.Id,entity.ProfileId,verdict.ToString(),summary,violations,entity.EvaluatedAtUtc);
    }
    private static PerformanceBudgetRule ValidateRule(PerformanceBudgetRule rule){if(string.IsNullOrWhiteSpace(rule.Metric)||string.IsNullOrWhiteSpace(rule.Aggregation)||string.IsNullOrWhiteSpace(rule.Unit))throw new ArgumentException("Performance budget rule metric, aggregation, and unit are required.");if(rule.Minimum.HasValue&&rule.Maximum.HasValue&&rule.Minimum.Value>rule.Maximum.Value)throw new ArgumentException("Performance budget minimum cannot exceed maximum.");return rule with{Metric=rule.Metric.Trim(),Aggregation=rule.Aggregation.Trim(),Unit=rule.Unit.Trim(),WarningPercent=Math.Clamp(rule.WarningPercent,0m,50m)};}
    private static PerformanceBudgetProfileDto Map(PerformanceBudgetProfile x)=>new(x.Id,x.ProjectId,x.Key,x.Name,x.Platform,x.TargetProfile,x.Enabled,JsonSerializer.Deserialize<PerformanceBudgetRule[]>(x.RulesJson)??Array.Empty<PerformanceBudgetRule>());
}
