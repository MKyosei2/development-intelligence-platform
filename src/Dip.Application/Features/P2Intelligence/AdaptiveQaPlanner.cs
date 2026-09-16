// Purpose: Coordinates A da pt iv eQ aP la nn er application behavior for P2Intelligence.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

public sealed class AdaptiveQaPlanner
{
    private readonly IEvidenceRepository _evidence;
    private readonly IP2IntelligenceRepository _intelligence;
    private readonly IAdaptiveQaPlanRepository _plans;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly IChangeRiskRepository? _changeRisk;

    public AdaptiveQaPlanner(
        IEvidenceRepository evidence,
        IP2IntelligenceRepository intelligence,
        IAdaptiveQaPlanRepository plans,
        IUnitOfWork uow,
        ISystemClock clock,
        IChangeRiskRepository? changeRisk=null)
    {
_evidence=evidence;_intelligence=intelligence;_plans=plans;_uow=uow;_clock=clock;_changeRisk=changeRisk;
    }

    public async Task<AdaptiveQaPlanDto> PlanAsync(AdaptiveQaPlanCommand command,CancellationToken ct)
    {
        var changed=command.ChangedFiles
            .Where(x=>!string.IsNullOrWhiteSpace(x))
            .Select(x=>x.Replace('\\','/').Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x=>x,StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if(changed.Length==0)throw new ArgumentException("At least one changed file is required.",nameof(command));

        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n",changed)))).ToLowerInvariant();
        var history=await _evidence.ListForProjectAsync(command.ProjectId,_clock.UtcNow.AddDays(-90),ct);
        var recentFailures=history.Where(x=>x.Outcome==EvidenceOutcome.Failed||x.Outcome==EvidenceOutcome.Warning).ToArray();
        var latestChangeRisk=_changeRisk is null?null:await _changeRisk.GetLatestAsync(command.ProjectId,null,ct);

        var steps=new Dictionary<string,AdaptiveQaStep>(StringComparer.OrdinalIgnoreCase);
        void Add(string id,string name,string category,string reason,decimal baseRisk,decimal minutes,bool required=false)
        {
            var historical=recentFailures.Count(e =>
                e.EvidenceKind.Contains(category,StringComparison.OrdinalIgnoreCase)
                ||(!string.IsNullOrWhiteSpace(e.SourceReference)&&changed.Any(f=>e.SourceReference!.Contains(Path.GetFileName(f),StringComparison.OrdinalIgnoreCase))));
            var risk=Math.Clamp(baseRisk+Math.Min(25m,historical*4m),0m,100m);
            var priority=risk>=80m?1:risk>=60m?2:risk>=40m?3:4;
            var step=new AdaptiveQaStep(id,name,category,reason+(historical>0?$" Historical evidence contains {historical} related warning/failure(s).":""),risk,minutes,priority,required||risk>=75m);
            if(!steps.TryGetValue(id,out var existing)||step.RiskScore>existing.RiskScore)steps[id]=step;
        }

        var docsOnly=changed.All(IsDocumentation);
        if(docsOnly)
        {
            Add("repo-contract","Repository contract checks","repository","Only documentation/configuration changed; full runtime QA is not justified.",20m,1m,true);
        }
        else
        {
            Add("build","Build / syntax validation","build","Source code changed.",60m,3m,true);
            Add("test-impact","Test impact selection","test-impact","Select tests from changed projects, paths and symbol/name overlap before executing the targeted suite.",68m,2m,true);
            Add("unit-tests","Targeted automated tests","test","Source changes should be checked against automated tests selected by impact analysis.",55m,5m,true);

            if(changed.Any(x=>x.EndsWith(".py",StringComparison.OrdinalIgnoreCase)))
                Add("python-qa","Python hardcoded QA","python","Python files changed.",65m,4m,true);
            if(changed.Any(x=>x.EndsWith(".cs",StringComparison.OrdinalIgnoreCase)||x.EndsWith(".csproj",StringComparison.OrdinalIgnoreCase)))
                Add("csharp-qa","C# hardcoded QA","csharp","C# files or project files changed.",65m,5m,true);
            if(changed.Any(x=>new[]{".cpp",".cc",".cxx",".h",".hpp",".hlsl",".glsl"}.Any(ext=>x.EndsWith(ext,StringComparison.OrdinalIgnoreCase))))
                Add("cpp-qa","C++ hardcoded QA + memory safety","cpp","Native or shader-adjacent code changed.",75m,8m,true);

            if(changed.Any(ContainsRendering))
            {
                Add("runtime","Runtime reproduction","runtime","Rendering/gameplay-facing code changed.",72m,8m,true);
                Add("visual-regression","Visual regression","visual","Rendering, shader, UI, scene or art paths changed.",78m,6m,true);
                Add("gpu-performance","GPU/performance target profiles","performance","Rendering changes can affect frame time and GPU headroom.",80m,10m,true);
            }
            if(changed.Any(ContainsMobile))
            {
                Add("mobile-performance","Mobile/low capability simulation","performance","Mobile/Android/iOS path changed.",85m,12m,true);
                Add("thermal","Thermal degradation run","thermal","Mobile targets are sensitive to sustained thermal throttling.",78m,30m);
                Add("memory-pressure","Memory pressure run","memory","Mobile targets have tighter memory budgets.",82m,10m,true);
            }
            if(changed.Any(ContainsAudio))
                Add("audio-regression","Audio regression","audio","Audio assets or audio code changed.",72m,6m,true);
            if(changed.Any(x=>ContainsUiOrArt(x)))
                Add("visual-ui","UI/art visual regression","visual","UI or art assets changed.",70m,5m,true);
            if(changed.Any(ContainsNetwork))
            {
                Add("network-fuzz","Protocol/API fuzzing","fuzz","Network/serialization code changed.",65m,12m);
                Add("runtime-repeat","Runtime reliability repeats","reliability","Network behavior can be timing-dependent.",62m,10m);
            }
            if(latestChangeRisk?.RiskLevel is ChangeRiskLevel.High or ChangeRiskLevel.Critical)
            {
                Add("game-replay","Deterministic game-session regression replay","runtime",$"Change Risk is {latestChangeRisk.RiskLevel} ({latestChangeRisk.RiskScore:0.#}/100).",88m,12m,true);
                Add("performance-budget","Platform performance budget gate","performance","High-risk changes require target-platform budget evidence.",86m,8m,true);
                Add("high-risk-fuzz","High-risk fuzzing","fuzz","High-risk change requires additional robustness evidence.",80m,12m,latestChangeRisk.RiskLevel==ChangeRiskLevel.Critical);
                Add("high-risk-soak","High-risk soak/reliability","reliability","Critical/high change risk warrants sustained execution.",82m,20m,latestChangeRisk.RiskLevel==ChangeRiskLevel.Critical);
            }
        }

        var ordered=steps.Values.OrderBy(x=>x.Priority).ThenByDescending(x=>x.RiskScore).ThenBy(x=>x.EstimatedMinutes).ToList();
        var selected=new List<AdaptiveQaStep>();
        var skipped=new List<string>();
        decimal total=0m;
        foreach(var step in ordered)
        {
            if(step.Required||total+step.EstimatedMinutes<=Math.Max(1m,command.MaxEstimatedMinutes))
            {
                selected.Add(step);total+=step.EstimatedMinutes;
            }
            else skipped.Add($"{step.Name}: skipped by QA time budget ({step.EstimatedMinutes:0.#} min).");
        }

        var risk=selected.Count==0?0m:decimal.Round(selected.Max(x=>x.RiskScore)*.6m+selected.Average(x=>x.RiskScore)*.4m,2);
        var id=Guid.NewGuid();
        var dto=new AdaptiveQaPlanDto(id,hash,risk,total,selected,skipped);
        var json=JsonSerializer.Serialize(dto);
        await _plans.AddAsync(new AdaptiveQaPlanSnapshot(id,command.ProjectId,hash,risk,total,json,_clock.UtcNow),ct);
        await _intelligence.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.AdaptiveQaPlan,P2IntelligenceStatus.Informational,
            risk,EvidenceConfidence.High,
            $"Adaptive QA selected {selected.Count} step(s), estimated {total:0.#} minutes, risk score {risk:0.#}/100.",
            json,null,_clock.UtcNow),ct);
        await _intelligence.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.QaCostEstimate,P2IntelligenceStatus.Informational,
            null,EvidenceConfidence.High,
            $"Estimated QA execution cost is {total:0.#} minutes for this change set.",
            JsonSerializer.Serialize(new{estimatedMinutes=total,steps=selected.Select(x=>new{x.Id,x.EstimatedMinutes,x.Priority})}),null,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);
        return dto;
    }

    private static bool IsDocumentation(string path)
    {
        var ext=Path.GetExtension(path);
        return path.StartsWith("docs/",StringComparison.OrdinalIgnoreCase)
            ||new[]{".md",".txt",".rst",".adoc"}.Contains(ext,StringComparer.OrdinalIgnoreCase);
    }
    private static bool ContainsRendering(string p)=>ContainsAny(p,"render","shader","material","texture","scene","camera","animation","particle","vfx","lighting","ui/");
    private static bool ContainsMobile(string p)=>ContainsAny(p,"android","ios","mobile","arm64","vulkan","metal");
    private static bool ContainsAudio(string p)=>ContainsAny(p,"audio","sound","music","bgm","se/","voice");
    private static bool ContainsUiOrArt(string p)=>ContainsAny(p,"ui/","art/","assets/","texture","material","prefab","scene");
    private static bool ContainsNetwork(string p)=>ContainsAny(p,"network","socket","packet","protocol","serialize","api/","server");
    private static bool ContainsAny(string p,params string[] tokens)=>tokens.Any(t=>p.Contains(t,StringComparison.OrdinalIgnoreCase));
}
