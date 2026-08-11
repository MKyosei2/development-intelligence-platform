using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.ChangeRisk;
public sealed record ChangeRiskRequest(Guid ProjectId,string CommitSha,int ChangedFiles,int ChangedLines,decimal? DirectTestCoveragePercent,int RelatedIncidents,int RecentRegressionCount,IReadOnlyList<string> ChangedPaths,bool TouchesPerformanceHotPath,bool TouchesSecuritySensitivePath,bool TouchesSchema,Guid? SourceRepositoryId=null);
public sealed record ChangeRiskDto(Guid Id,string CommitSha,decimal RiskScore,string RiskLevel,IReadOnlyList<string> Reasons,IReadOnlyList<Guid> AffectedRepositories,DateTimeOffset AssessedAtUtc);

public sealed class ChangeRiskIntelligenceService
{
    private readonly IChangeRiskRepository _repo;private readonly IRepositoryGraphRepository _graph;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    public ChangeRiskIntelligenceService(IChangeRiskRepository repo,IRepositoryGraphRepository graph,IUnitOfWork uow,ISystemClock clock){_repo=repo;_graph=graph;_uow=uow;_clock=clock;}
    public async Task<ChangeRiskDto> AssessAsync(ChangeRiskRequest r,CancellationToken ct)
    {
        var reasons=new List<string>();decimal score=0;score+=Math.Min(25m,r.ChangedFiles*1.5m);if(r.ChangedFiles>=10)reasons.Add($"{r.ChangedFiles} files changed.");score+=Math.Min(20m,r.ChangedLines/50m);if(r.ChangedLines>=500)reasons.Add($"Large change set: {r.ChangedLines} lines.");var coveragePenalty=r.DirectTestCoveragePercent.HasValue?Math.Clamp((70m-r.DirectTestCoveragePercent.Value)/70m*20m,0m,20m):0m;score+=coveragePenalty;if(coveragePenalty>=8)reasons.Add($"Direct test coverage is {r.DirectTestCoveragePercent:0.#}%.");else if(!r.DirectTestCoveragePercent.HasValue)reasons.Add("Direct test coverage was not supplied; no coverage penalty was inferred.");score+=Math.Min(15m,r.RelatedIncidents*4m);if(r.RelatedIncidents>0)reasons.Add($"{r.RelatedIncidents} related incident(s) exist.");score+=Math.Min(10m,r.RecentRegressionCount*3m);if(r.RecentRegressionCount>0)reasons.Add($"{r.RecentRegressionCount} recent regression(s) are associated with this area.");if(r.TouchesPerformanceHotPath){score+=12;reasons.Add("Performance-sensitive path changed.");}if(r.TouchesSecuritySensitivePath){score+=12;reasons.Add("Security-sensitive path changed.");}if(r.TouchesSchema){score+=10;reasons.Add("Schema or contract changed.");}
        var affected=new List<Guid>();if(r.SourceRepositoryId.HasValue){var impact=await _graph.GetLatestImpactAsync(r.ProjectId,r.CommitSha,ct);if(impact is not null){affected=JsonSerializer.Deserialize<List<Guid>>(impact.AffectedRepositoryIdsJson)??[];score+=impact.Level switch{CrossRepositoryImpactLevel.Critical=>15,CrossRepositoryImpactLevel.High=>10,CrossRepositoryImpactLevel.Medium=>6,CrossRepositoryImpactLevel.Low=>3,_=>0};if(affected.Count>0)reasons.Add($"Cross-repository impact reaches {affected.Count} repository/repositories ({impact.Level}).");}}
        score=Math.Clamp(decimal.Round(score,2),0m,100m);var level=score>=85?ChangeRiskLevel.Critical:score>=65?ChangeRiskLevel.High:score>=35?ChangeRiskLevel.Medium:ChangeRiskLevel.Low;if(reasons.Count==0)reasons.Add("No elevated deterministic risk factor was detected from the supplied change evidence.");var entity=new ChangeRiskAssessment(Guid.NewGuid(),r.ProjectId,r.CommitSha,score,level,JsonSerializer.Serialize(reasons),JsonSerializer.Serialize(affected),_clock.UtcNow);await _repo.AddAsync(entity,ct);await _uow.SaveChangesAsync(ct);return new(entity.Id,entity.CommitSha,entity.RiskScore,entity.RiskLevel.ToString(),reasons,affected,entity.AssessedAtUtc);
    }
    public async Task<ChangeRiskDto?> GetLatestAsync(Guid projectId,string? commit,CancellationToken ct){var e=await _repo.GetLatestAsync(projectId,commit,ct);return e is null?null:new(e.Id,e.CommitSha,e.RiskScore,e.RiskLevel.ToString(),JsonSerializer.Deserialize<string[]>(e.ReasonsJson)??[],JsonSerializer.Deserialize<Guid[]>(e.AffectedRepositoriesJson)??[],e.AssessedAtUtc);}
}
