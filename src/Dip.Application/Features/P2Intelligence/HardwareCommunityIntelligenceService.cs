// Purpose: Coordinates H ar dw ar eC om mu ni ty In te ll ig en ce Se rv ic e application behavior for P2Intelligence.
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.P2Intelligence;

public sealed class HardwareCommunityIntelligenceService
{
    private readonly IProjectRepository _projects;
    private readonly IHardwareCommunityRepository _community;
    private readonly IHardwareProfileRepository _profiles;
    private readonly IP2IntelligenceRepository _intelligence;
    private readonly IEvidenceRepository _evidence;
    private readonly IUnitOfWork _uow;
    private readonly ISystemClock _clock;
    private readonly ProjectTruthService _truth;

    public HardwareCommunityIntelligenceService(
        IProjectRepository projects,
        IHardwareCommunityRepository community,
        IHardwareProfileRepository profiles,
        IP2IntelligenceRepository intelligence,
        IEvidenceRepository evidence,
        IUnitOfWork uow,
        ISystemClock clock,
        ProjectTruthService truth)
    {
        _projects=projects;_community=community;_profiles=profiles;_intelligence=intelligence;
        _evidence=evidence;_uow=uow;_clock=clock;_truth=truth;
    }

    public async Task<HardwareProfileConfidenceDto> SubmitAsync(CommunityHardwareSubmission command,CancellationToken ct)
    {
        _=await _projects.GetByIdAsync(command.ProjectId,ct)
            ??throw new KeyNotFoundException($"Project {command.ProjectId} not found.");
        if(!command.ConsentGranted)
            throw new InvalidOperationException("Community calibration upload is opt-in only. ConsentGranted must be true.");
        if(string.IsNullOrWhiteSpace(command.HardwareKey))
            throw new ArgumentException("HardwareKey is required only for local hashing and deduplication.",nameof(command));

        var hash=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(command.HardwareKey.Trim()))).ToLowerInvariant();
        var sample=new HardwareCommunitySample(
            Guid.NewGuid(),command.ProfileKey,command.Platform,hash,command.OsFamily,command.DriverFamily,
            command.CpuSingleScore,command.CpuMultiScore,command.GpuScore,command.MemoryScore,command.StorageScore,
            command.ThermalRetentionPercent,command.AvailableMemoryMb,command.RealDevice,true,command.MeasuredAtUtc,_clock.UtcNow);
        await _community.AddAsync(sample,ct);
        await _uow.SaveChangesAsync(ct);

        var confidence=await GetConfidenceAsync(command.ProfileKey,command.Platform,ct);
        var payload=JsonSerializer.Serialize(new
        {
            command.ProfileKey,command.Platform,
            confidence.SampleCount,confidence.RealDeviceSampleCount,confidence.Confidence,confidence.ConfidencePercent,
            privacy=new{optIn=true,rawHardwareKeyStored=false,accountIdentityStored=false,ipStored=false}
        });
        var record=new P2IntelligenceRecord(
            Guid.NewGuid(),command.ProjectId,"hardware",P2IntelligenceKind.CommunityHardware,P2IntelligenceStatus.Informational,
            confidence.ConfidencePercent,ParseConfidence(confidence.Confidence),
            $"Community hardware profile {command.ProfileKey} now has {confidence.SampleCount} privacy-minimised sample(s).",
            payload,null,_clock.UtcNow);
        await _intelligence.AddAsync(record,ct);

        // Community data informs planning. It is not direct proof that this project ran on that device.
        await _evidence.AddAsync(new EvidenceRecord(
            Guid.NewGuid(),command.ProjectId,null,"hardware",EvidenceSourceType.CommunityHardware,
            "hardware.community-profile",EvidenceOutcome.Informational,
            $"Community profile updated: {command.ProfileKey}",
            $"An opt-in aggregated hardware profile is available with {confidence.SampleCount} sample(s). This is capability evidence, not project execution evidence.",
            EvidenceConfidence.Medium,null,payload,_clock.UtcNow,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);
        await _truth.RecalculateAsync(command.ProjectId,ct);
        return confidence;
    }

    public async Task<HardwareProfileConfidenceDto> GetConfidenceAsync(string profileKey,string platform,CancellationToken ct)
    {
        var samples=await _community.ListAsync(profileKey,platform,10000,ct);
        if(samples.Count==0)
            return new(profileKey,platform,0,0,0,0,0,0,0,0,0,0,0,0,"UNVERIFIED",0m,null);

        var real=samples.Count(x=>x.RealDevice);
        var os=samples.Select(x=>x.OsFamily).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var drivers=samples.Select(x=>x.DriverFamily).Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var gpu=samples.Select(x=>x.GpuScore).OrderBy(x=>x).ToArray();
        var confidence=ComputeConfidence(samples.Count,real,os,drivers,Variation(gpu),samples.Max(x=>x.MeasuredAtUtc),_clock.UtcNow);
        return new(
            profileKey,platform,samples.Count,real,os,drivers,
            Median(samples.Select(x=>x.CpuSingleScore)),
            Median(samples.Select(x=>x.CpuMultiScore)),
            Median(gpu),
            Median(samples.Select(x=>x.MemoryScore)),
            Median(samples.Select(x=>x.StorageScore)),
            Median(samples.Select(x=>x.ThermalRetentionPercent)),
            Percentile(gpu,.10m),Percentile(gpu,.90m),
            confidence.Name,confidence.Percent,
            samples.Max(x=>x.MeasuredAtUtc));
    }

    public async Task<Dip.Application.Features.Hardware.HardwareProfileDto> CreateCommunityProfileAsync(
        CreateCommunityHardwareProfileCommand command,CancellationToken ct)
    {
        _=await _projects.GetByIdAsync(command.ProjectId,ct)
            ??throw new KeyNotFoundException($"Project {command.ProjectId} not found.");
        var samples=await _community.ListAsync(command.ProfileKey,command.Platform,10000,ct);
        if(samples.Count<3)throw new InvalidOperationException("At least three opt-in community samples are required to create an aggregate hardware profile.");
        var confidence=await GetConfidenceAsync(command.ProfileKey,command.Platform,ct);
        var existing=await _profiles.FindAsync(command.Name,command.Platform,ct);
        if(existing is not null)return MapProfile(existing);
        var memoryBudget=(long)Median(samples.Select(x=>(decimal)x.AvailableMemoryMb));
        var metadata=JsonSerializer.Serialize(new
        {
            kind="community-aggregate",
            command.ProfileKey,command.Platform,
            sampleCount=samples.Count,realDeviceSamples=samples.Count(x=>x.RealDevice),
            confidence=confidence.Confidence,confidencePercent=confidence.ConfidencePercent,
            privacy="aggregate-only"
        });
        var profile=new HardwareProfile(
            Guid.NewGuid(),command.Name,command.Platform,HardwareProfileSource.CommunityAggregate,
            confidence.MedianCpuSingle,confidence.MedianCpuMulti,confidence.MedianGpu,
            confidence.MedianMemory,confidence.MedianStorage,confidence.MedianThermalRetention,
            Math.Max(128,memoryBudget),Math.Clamp(command.TargetFps,1,1000),metadata,_clock.UtcNow);
        await _profiles.AddAsync(profile,ct);
        await _p2Record(command,confidence,profile,metadata,ct);
        await _uow.SaveChangesAsync(ct);
        return MapProfile(profile);
    }

    private async Task _p2Record(
        CreateCommunityHardwareProfileCommand command,HardwareProfileConfidenceDto confidence,HardwareProfile profile,string metadata,CancellationToken ct)
    {
        await _intelligence.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),command.ProjectId,"hardware",P2IntelligenceKind.HardwareProfileConfidence,P2IntelligenceStatus.Informational,
            confidence.ConfidencePercent,ParseConfidence(confidence.Confidence),
            $"Community aggregate profile '{profile.Name}' created from {confidence.SampleCount} sample(s), confidence {confidence.Confidence}.",
            metadata,null,_clock.UtcNow),ct);
    }

    public async Task<PortabilityResult> AnalyzePortabilityAsync(PortabilityCommand command,CancellationToken ct)
    {
        var source=await _profiles.GetAsync(command.SourceHardwareProfileId,ct)??throw new KeyNotFoundException("Source hardware profile not found.");
        var target=await _profiles.GetAsync(command.TargetHardwareProfileId,ct)??throw new KeyNotFoundException("Target hardware profile not found.");
        _=source; // Source is retained in payload to explain the transition.

        var dims=new[]
        {
            Dimension("CPU",target.CpuSingleScore,command.RequiredCpuScore),
            Dimension("GPU",target.GpuScore,command.RequiredGpuScore),
            Dimension("Memory bandwidth",target.MemoryScore,command.RequiredMemoryScore),
            Dimension("Storage",target.StorageScore,command.RequiredStorageScore),
            Dimension("Free memory",target.MemoryBudgetMb,command.RequiredMemoryMb),
            Dimension("Thermal retention",target.ThermalScore,command.RequiredThermalRetention)
        };
        var score=decimal.Round(dims.Average(x=>Math.Min(1m,x.Ratio))*100m,2);
        var blockers=dims.Where(x=>x.Ratio<1m).OrderBy(x=>x.Ratio).Select(x=>$"{x.Name}: {x.Available:0.##} < {x.Required:0.##}").ToArray();
        var status=blockers.Length==0?"PASS":score>=70m?"WARNING":"FAIL";
        var confidence=target.Source==HardwareProfileSource.RealDevice?"HIGH":"MEDIUM";
        var result=new PortabilityResult(score,status,confidence,dims,blockers);

        var payload=JsonSerializer.Serialize(new
        {
            source=new{source.Id,source.Name,source.Platform},
            target=new{target.Id,target.Name,target.Platform,target.Source},
            result.Score,result.Status,result.Confidence,result.Dimensions,result.Blockers
        });
        var p2Status=status=="PASS"?P2IntelligenceStatus.Passed:status=="WARNING"?P2IntelligenceStatus.Warning:P2IntelligenceStatus.Failed;
        await _intelligence.AddAsync(new P2IntelligenceRecord(
            Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.Portability,p2Status,score,
            confidence=="HIGH"?EvidenceConfidence.High:EvidenceConfidence.Medium,
            $"Portability from {source.Name} to {target.Name}: {status} ({score:0.#}/100).",
            payload,null,_clock.UtcNow),ct);

        var evidenceOutcome=status=="FAIL"?EvidenceOutcome.Warning:status=="WARNING"?EvidenceOutcome.Warning:EvidenceOutcome.Informational;
        await _evidence.AddAsync(new EvidenceRecord(
            Guid.NewGuid(),command.ProjectId,null,command.SubjectKey,EvidenceSourceType.Analysis,
            "portability.analysis",evidenceOutcome,
            $"Portability analysis: {status}",
            blockers.Length==0
                ?$"No target capability deficit was found in the configured requirements for {target.Name}."
                :$"{blockers.Length} target capability deficit(s) were found. This is planning analysis, not real-device execution.",
            EvidenceConfidence.Medium,null,payload,_clock.UtcNow,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);
        await _truth.RecalculateAsync(command.ProjectId,ct);
        return result;
    }

    private static PortabilityDimension Dimension(string name,decimal available,decimal required)
    {
        var ratio=required<=0m?1m:available/required;
        var status=ratio>=1m?"PASS":ratio>=.85m?"WARNING":"FAIL";
        return new(name,available,required,decimal.Round(ratio,3),status,
            ratio>=1m?"Target capability meets the configured requirement.":"Target capability is below the configured requirement.");
    }

    private static (string Name,decimal Percent) ComputeConfidence(int count,int real,int os,int drivers,decimal variation,DateTimeOffset latest,DateTimeOffset now)
    {
        var sampleScore=Math.Min(45m,(decimal)Math.Log10(Math.Max(1,count)+1)*20m);
        var realScore=count==0?0m:Math.Min(25m,(decimal)real/count*25m);
        var diversityScore=Math.Min(15m,os*4m+drivers*2m);
        var varianceScore=Math.Max(0m,10m-Math.Min(10m,variation*20m));
        var ageDays=(decimal)Math.Max(0,(now-latest).TotalDays);
        var recency=Math.Max(0m,5m-Math.Min(5m,ageDays/30m*5m));
        var percent=Math.Clamp(decimal.Round(sampleScore+realScore+diversityScore+varianceScore+recency,2),0m,100m);
        var name=percent>=80m?"HIGH":percent>=55m?"MEDIUM":percent>=30m?"LOW":"UNVERIFIED";
        return(name,percent);
    }

    private static decimal Median(IEnumerable<decimal> values)
    {
        var a=values.OrderBy(x=>x).ToArray();if(a.Length==0)return 0m;
        return a.Length%2==1?a[a.Length/2]:decimal.Round((a[a.Length/2-1]+a[a.Length/2])/2m,3);
    }
    private static decimal Percentile(IReadOnlyList<decimal> ordered,decimal p)
    {
        if(ordered.Count==0)return 0m;
        var index=(int)Math.Floor((ordered.Count-1)*(double)p);
        return ordered[Math.Clamp(index,0,ordered.Count-1)];
    }
    private static decimal Variation(IReadOnlyList<decimal> ordered)
    {
        if(ordered.Count<2)return 0m;var mean=ordered.Average();if(mean==0m)return 0m;
        var variance=ordered.Select(x=>(double)((x-mean)*(x-mean))).Average();
        return (decimal)Math.Sqrt(variance)/(decimal)mean;
    }
    private static Dip.Application.Features.Hardware.HardwareProfileDto MapProfile(HardwareProfile p)=>new(
        p.Id,p.Name,p.Platform,p.Source.ToString(),p.CpuSingleScore,p.CpuMultiScore,p.GpuScore,p.MemoryScore,p.StorageScore,p.ThermalScore,p.MemoryBudgetMb,p.TargetFps,p.MetadataJson);
    private static EvidenceConfidence ParseConfidence(string value)=>value switch
    {
        "HIGH"=>EvidenceConfidence.High,
        "MEDIUM"=>EvidenceConfidence.Medium,
        "LOW"=>EvidenceConfidence.Low,
        _=>EvidenceConfidence.Unverified
    };
}
