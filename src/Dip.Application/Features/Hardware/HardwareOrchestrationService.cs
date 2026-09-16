// Purpose: Coordinates H ar dw ar eO rc he st ra ti on Se rv ic e application behavior for Hardware.
using System.Text.Json;
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Abstractions.Time;
using Dip.Application.Features.ProjectTruth;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Application.Features.Hardware;

public sealed class HardwareOrchestrationService
{
    private readonly IProjectRepository _projects;private readonly IHardwareProfileRepository _profiles;private readonly IHardwareCalibrationRepository _calibrations;
    private readonly IPerformanceSimulationRepository _simulations;private readonly IEvidenceRepository _evidence;private readonly IP2IntelligenceRepository _p2;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    private readonly IHardwareCalibrationRunner _calibrator;private readonly IPerformanceSimulationRunner _runner;private readonly IQaWorkspaceResolver _workspace;private readonly ProjectTruthService _truth;
    public HardwareOrchestrationService(IProjectRepository projects,IHardwareProfileRepository profiles,IHardwareCalibrationRepository calibrations,IPerformanceSimulationRepository simulations,
        IEvidenceRepository evidence,IP2IntelligenceRepository p2,IUnitOfWork uow,ISystemClock clock,IHardwareCalibrationRunner calibrator,IPerformanceSimulationRunner runner,IQaWorkspaceResolver workspace,ProjectTruthService truth)
    {_projects=projects;_profiles=profiles;_calibrations=calibrations;_simulations=simulations;_evidence=evidence;_p2=p2;_uow=uow;_clock=clock;_calibrator=calibrator;_runner=runner;_workspace=workspace;_truth=truth;}

    public async Task<HardwareCalibrationMeasurement> CalibrateAsync(Guid projectId,CancellationToken ct)
    {
        _=await _projects.GetByIdAsync(projectId,ct)??throw new KeyNotFoundException($"Project {projectId} not found.");
        var dir=Path.Combine(Path.GetFullPath("artifacts/hardware"),projectId.ToString("N"),$"calibration-{_clock.UtcNow:yyyyMMdd-HHmmss}");Directory.CreateDirectory(dir);
        var m=await _calibrator.CalibrateAsync(dir,ct);
        await _calibrations.AddAsync(new HardwareCalibration(Guid.NewGuid(),projectId,m.MachineFingerprint,m.CpuSingleScore,m.CpuMultiScore,m.GpuScore,m.MemoryScore,m.StorageScore,m.AvailableMemoryMb,m.EnvironmentJson,_clock.UtcNow),ct);
        var calibrationConfidence=m.GpuScore>0?EvidenceConfidence.Verified:EvidenceConfidence.High;
        await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),projectId,null,"project",EvidenceSourceType.Performance,"hardware.calibration",EvidenceOutcome.Informational,"QA machine calibrated",
            "The current QA machine was benchmarked for CPU, memory and storage, with GPU calibration when the native probe is available. These measurements describe this machine; they do not prove equivalence to another device.",calibrationConfidence,null,m.RawResultJson,_clock.UtcNow,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);await _truth.RecalculateAsync(projectId,ct);return m;
    }

    public async Task<HardwareProfileDto> CreateRelativeProfileAsync(CreateRelativeHardwareProfileCommand command,CancellationToken ct)
    {
        _=await _projects.GetByIdAsync(command.ProjectId,ct)??throw new KeyNotFoundException($"Project {command.ProjectId} not found.");
        var baseline=await _calibrations.GetLatestAsync(command.ProjectId,ct)??throw new InvalidOperationException("Run hardware calibration before creating a relative target profile.");
        static decimal R(decimal score,decimal percent)=>decimal.Round(score*Math.Clamp(percent,1m,200m)/100m,3);
        var metadata=JsonSerializer.Serialize(new{kind="relative-to-local-calibration",baseline.MachineFingerprint,command.CpuPercent,command.GpuPercent,command.MemoryBandwidthPercent,command.StoragePercent,command.ThermalRetentionPercent});
        var existing=await _profiles.FindAsync(command.Name,command.Platform,ct);if(existing is not null)return Map(existing);
        var profile=new HardwareProfile(Guid.NewGuid(),command.Name,command.Platform,HardwareProfileSource.SimulationTarget,R(baseline.CpuSingleScore,command.CpuPercent),R(baseline.CpuMultiScore,command.CpuPercent),
            R(baseline.GpuScore,command.GpuPercent),R(baseline.MemoryScore,command.MemoryBandwidthPercent),R(baseline.StorageScore,command.StoragePercent),Math.Clamp(command.ThermalRetentionPercent,1m,100m),command.MemoryBudgetMb,command.TargetFps,metadata,_clock.UtcNow);
        await _profiles.AddAsync(profile,ct);await _uow.SaveChangesAsync(ct);return Map(profile);
    }

    public async Task<IReadOnlyList<HardwareProfileDto>> ListProfilesAsync(CancellationToken ct)=>(await _profiles.ListAsync(ct)).Select(Map).ToArray();

    public async Task<PerformanceSimulationDto> SimulateAsync(PerformanceSimulationCommand command,CancellationToken ct)
    {
        _=await _projects.GetByIdAsync(command.ProjectId,ct)??throw new KeyNotFoundException($"Project {command.ProjectId} not found.");
        var profile=await _profiles.GetAsync(command.HardwareProfileId,ct)??throw new KeyNotFoundException("Hardware profile not found.");
        var workspace=_workspace.Resolve(command.WorkspaceRelativePath);
        if(Path.IsPathRooted(command.Executable))throw new InvalidOperationException("Simulation executable must be workspace-relative.");
        var executable=Path.GetFullPath(Path.Combine(workspace,command.Executable));
        var prefix=workspace.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!executable.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("Simulation executable escapes the configured workspace.");

        var runId=Guid.NewGuid();
        var artifactDir=Path.Combine(Path.GetFullPath("artifacts/hardware"),command.ProjectId.ToString("N"),$"simulation-{runId:N}");
        Directory.CreateDirectory(artifactDir);
        var run=new PerformanceSimulationRun(runId,command.ProjectId,profile.Id,command.SubjectKey,artifactDir,_clock.UtcNow);
        await _simulations.AddAsync(run,ct);run.Start(_clock.UtcNow);await _uow.SaveChangesAsync(ct);

        PerformanceSimulationMeasurement measurement;
        try
        {
            measurement=await _runner.RunAsync(Map(profile),workspace,executable,command.Arguments??Array.Empty<string>(),Math.Clamp(command.DurationSeconds,5,3600),command.ThermalSimulation,artifactDir,ct);
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested)
        {
            var cancelled=JsonSerializer.Serialize(new{status="Cancelled",simulated=true,realDevice=false,reason="The caller cancelled the simulation."});
            run.Complete(SimulationStatus.Cancelled,0m,cancelled,_clock.UtcNow);
            await _uow.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch(Exception ex)
        {
            var failure=JsonSerializer.Serialize(new{status="InfrastructureFailure",simulated=true,realDevice=false,error=ex.Message,type=ex.GetType().FullName});
            run.Complete(SimulationStatus.InfrastructureFailure,0m,failure,_clock.UtcNow);
            await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),command.ProjectId,null,command.SubjectKey,EvidenceSourceType.System,"hardware.simulation.infrastructure",EvidenceOutcome.Unknown,
                "Hardware simulation infrastructure failed","The requested simulation could not be completed, so this result must not be interpreted as a product failure.",EvidenceConfidence.High,null,failure,_clock.UtcNow,_clock.UtcNow),ct);
            await _uow.SaveChangesAsync(ct);await _truth.RecalculateAsync(command.ProjectId,ct);
            return new(run.Id,SimulationStatus.InfrastructureFailure.ToString(),0m,failure,$"artifacts/hardware/{command.ProjectId:N}/simulation-{run.Id:N}");
        }

        var status=Enum.TryParse<SimulationStatus>(measurement.Status,true,out var parsed)?parsed:SimulationStatus.InfrastructureFailure;
        run.Complete(status,measurement.ConfidencePercent,measurement.ResultJson,_clock.UtcNow);
        var outcome=status==SimulationStatus.Failed?EvidenceOutcome.Failed:status==SimulationStatus.Warning?EvidenceOutcome.Warning:status==SimulationStatus.Passed?EvidenceOutcome.Succeeded:EvidenceOutcome.Unknown;
        var confidence=measurement.ConfidencePercent>=75?EvidenceConfidence.High:measurement.ConfidencePercent>=45?EvidenceConfidence.Medium:EvidenceConfidence.Low;
        await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),command.ProjectId,null,command.SubjectKey,EvidenceSourceType.Performance,"hardware.simulation",outcome,
            $"{profile.Name} performance simulation {status}",$"Simulation against {profile.Name}: peak memory {measurement.PeakWorkingSetMb} MB, FPS {(measurement.AverageFps?.ToString("0.##")??"not reported")}. This is simulated evidence, not a real-device measurement.",confidence,null,measurement.ResultJson,_clock.UtcNow,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);await _truth.RecalculateAsync(command.ProjectId,ct);
        var artifactReference=$"artifacts/hardware/{command.ProjectId:N}/simulation-{run.Id:N}";
        return new(run.Id,status.ToString(),measurement.ConfidencePercent,measurement.ResultJson,artifactReference);
    }

    public async Task<MinimumHardwareEstimateDto> EstimateMinimumAsync(MinimumHardwareEstimateCommand command,CancellationToken ct)
    {
        _=await _projects.GetByIdAsync(command.ProjectId,ct)??throw new KeyNotFoundException($"Project {command.ProjectId} not found.");
        var baseline=await _calibrations.GetLatestAsync(command.ProjectId,ct)??throw new InvalidOperationException("Run hardware calibration first.");
        var workspace=_workspace.Resolve(command.WorkspaceRelativePath);
        if(Path.IsPathRooted(command.Executable))throw new InvalidOperationException("Minimum-hardware target executable must be workspace-relative.");
        var executable=Path.GetFullPath(Path.Combine(workspace,command.Executable));
        var prefix=workspace.TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar;
        if(!executable.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)||!File.Exists(executable))
            throw new InvalidOperationException("Minimum-hardware target executable escapes the configured workspace or does not exist.");

        var artifactRoot=Path.Combine(Path.GetFullPath("artifacts/hardware"),command.ProjectId.ToString("N"),$"minimum-p2-{Guid.NewGuid():N}");
        Directory.CreateDirectory(artifactRoot);
        var tested=new List<string>();

        async Task<(decimal Minimum,bool InfrastructureFailure)> SearchPercentAsync(string dimension,Func<decimal,HardwareProfileDto> profileFactory)
        {
            decimal low=10m,high=100m,best=100m;
            for(var i=0;i<7;i++)
            {
                var percent=i==0?100m:decimal.Round((low+high)/2m,2);
                var profile=profileFactory(percent);
                var dir=Path.Combine(artifactRoot,$"{dimension}-{i:D2}-{percent:0.##}");
                Directory.CreateDirectory(dir);
                PerformanceSimulationMeasurement measurement;
                try
                {
                    measurement=await _runner.RunAsync(profile,workspace,executable,command.Arguments??Array.Empty<string>(),
                        Math.Clamp(command.DurationSeconds,5,600),true,dir,ct);
                }
                catch(Exception ex) when(ex is not OperationCanceledException)
                {
                    tested.Add($"{dimension} {percent:0.##}% => InfrastructureFailure ({ex.GetType().Name})");
                    return(best,true);
                }
                tested.Add($"{dimension} {percent:0.##}% => {measurement.Status}");
                if(measurement.Status.Equals("InfrastructureFailure",StringComparison.OrdinalIgnoreCase)
                    ||measurement.Status.Equals("Unsupported",StringComparison.OrdinalIgnoreCase)
                    ||measurement.Status.Equals("Cancelled",StringComparison.OrdinalIgnoreCase))
                    return(best,true);
                var pass=measurement.Status.Equals("Passed",StringComparison.OrdinalIgnoreCase);
                if(pass){best=Math.Min(best,percent);high=percent;}else low=percent;
            }
            return(decimal.Round(best,2),false);
        }

        HardwareProfileDto Make(
            decimal cpuPercent,decimal gpuPercent,decimal memoryPercent,decimal storagePercent,long memoryBudget)=>
            new(Guid.Empty,"P2 Boundary","Estimated","SimulationTarget",
                baseline.CpuSingleScore*cpuPercent/100m,
                baseline.CpuMultiScore*cpuPercent/100m,
                baseline.GpuScore*gpuPercent/100m,
                baseline.MemoryScore*memoryPercent/100m,
                baseline.StorageScore*storagePercent/100m,
                65m,memoryBudget,command.TargetFps,
                JsonSerializer.Serialize(new{kind="p2-minimum-hardware",cpuPercent,gpuPercent,memoryPercent,storagePercent,memoryBudget,simulated=true,realDevice=false}));

        var cpu=await SearchPercentAsync("cpu",p=>Make(p,100m,100m,100m,command.MemoryBudgetMb));
        var gpu=baseline.GpuScore<=0m?(100m,false):await SearchPercentAsync("gpu",p=>Make(100m,p,100m,100m,command.MemoryBudgetMb));
        var memory=await SearchPercentAsync("memory-bandwidth",p=>Make(100m,100m,p,100m,command.MemoryBudgetMb));
        var storage=await SearchPercentAsync("storage",p=>Make(100m,100m,100m,p,command.MemoryBudgetMb));

        long lowMb=512,highMb=Math.Max(512,Math.Min(command.MemoryBudgetMb,baseline.AvailableMemoryMb));
        long bestMb=highMb;bool memoryInfrastructure=false;
        for(var i=0;i<6;i++)
        {
            var mb=i==0?highMb:(lowMb+highMb)/2;
            var profile=Make(100m,100m,100m,100m,mb);
            var dir=Path.Combine(artifactRoot,$"memory-budget-{i:D2}-{mb}");
            Directory.CreateDirectory(dir);
            PerformanceSimulationMeasurement measurement;
            try
            {
                measurement=await _runner.RunAsync(profile,workspace,executable,command.Arguments??Array.Empty<string>(),
                    Math.Clamp(command.DurationSeconds,5,600),true,dir,ct);
            }
            catch(Exception ex) when(ex is not OperationCanceledException)
            {
                tested.Add($"memory-budget {mb} MB => InfrastructureFailure ({ex.GetType().Name})");
                memoryInfrastructure=true;break;
            }
            tested.Add($"memory-budget {mb} MB => {measurement.Status}");
            if(measurement.Status.Equals("InfrastructureFailure",StringComparison.OrdinalIgnoreCase)
                ||measurement.Status.Equals("Unsupported",StringComparison.OrdinalIgnoreCase))
            {memoryInfrastructure=true;break;}
            var pass=measurement.Status.Equals("Passed",StringComparison.OrdinalIgnoreCase);
            if(pass){bestMb=Math.Min(bestMb,mb);highMb=mb;}else lowMb=Math.Min(highMb,mb+1);
        }

        var infra=cpu.InfrastructureFailure||gpu.Item2||memory.InfrastructureFailure||storage.InfrastructureFailure||memoryInfrastructure;
        if(infra)
        {
            var failed=new MinimumHardwareEstimateDto(cpu.Minimum,gpu.Item1,0m,"InfrastructureFailure",tested,
                "Boundary search stopped because at least one simulation did not produce product evidence. No certified minimum should be inferred.")
            {
                EstimatedMemoryBandwidthPercent=memory.Minimum,EstimatedStoragePercent=storage.Minimum,EstimatedMemoryBudgetMb=bestMb,
                RecommendedCpuPercent=cpu.Minimum,RecommendedGpuPercent=gpu.Item1,
                RecommendedMemoryBandwidthPercent=memory.Minimum,RecommendedStoragePercent=storage.Minimum,RecommendedMemoryBudgetMb=bestMb
            };
            var failureJson=JsonSerializer.Serialize(failed);
            await _p2.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.MinimumHardware,
                P2IntelligenceStatus.InfrastructureFailure,null,EvidenceConfidence.High,failed.EvidenceNote,failureJson,$"artifacts/hardware/{command.ProjectId:N}/{Path.GetFileName(artifactRoot)}",_clock.UtcNow),ct);
            await _uow.SaveChangesAsync(ct);
            return failed;
        }

        static decimal Recommended(decimal minimum)=>decimal.Round(Math.Min(100m,minimum*1.15m),2);
        var confidence=baseline.GpuScore>0m?78m:65m;
        var result=new MinimumHardwareEstimateDto(cpu.Minimum,gpu.Item1,confidence,"Estimated",tested,
            "P2 independently searched CPU, GPU, memory-bandwidth, storage and memory-budget boundaries. Results are simulated estimates; real-device evidence is required before publishing a certified minimum specification.")
        {
            EstimatedMemoryBandwidthPercent=memory.Minimum,
            EstimatedStoragePercent=storage.Minimum,
            EstimatedMemoryBudgetMb=bestMb,
            RecommendedCpuPercent=Recommended(cpu.Minimum),
            RecommendedGpuPercent=Recommended(gpu.Item1),
            RecommendedMemoryBandwidthPercent=Recommended(memory.Minimum),
            RecommendedStoragePercent=Recommended(storage.Minimum),
            RecommendedMemoryBudgetMb=(long)Math.Ceiling(bestMb*1.15m)
        };
        var json=JsonSerializer.Serialize(new{result,simulated=true,realDevice=false,baselineMachine=baseline.MachineFingerprint});
        await _p2.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.MinimumHardware,
            P2IntelligenceStatus.Informational,confidence,EvidenceConfidence.Medium,
            $"Estimated minimum boundary: CPU {result.EstimatedCpuPercent:0.#}%, GPU {result.EstimatedGpuPercent:0.#}%, memory bandwidth {result.EstimatedMemoryBandwidthPercent:0.#}%, storage {result.EstimatedStoragePercent:0.#}%, memory {result.EstimatedMemoryBudgetMb} MB.",
            json,$"artifacts/hardware/{command.ProjectId:N}/{Path.GetFileName(artifactRoot)}",_clock.UtcNow),ct);
        await _p2.AddAsync(new P2IntelligenceRecord(Guid.NewGuid(),command.ProjectId,command.SubjectKey,P2IntelligenceKind.SafeHardwareRecommendation,
            P2IntelligenceStatus.Informational,confidence,EvidenceConfidence.Medium,
            $"Recommended safety-margin profile: CPU {result.RecommendedCpuPercent:0.#}%, GPU {result.RecommendedGpuPercent:0.#}%, memory {result.RecommendedMemoryBudgetMb} MB.",
            json,$"artifacts/hardware/{command.ProjectId:N}/{Path.GetFileName(artifactRoot)}",_clock.UtcNow),ct);
        await _evidence.AddAsync(new EvidenceRecord(Guid.NewGuid(),command.ProjectId,null,command.SubjectKey,EvidenceSourceType.Performance,
            "hardware.minimum-estimate",EvidenceOutcome.Informational,"Estimated minimum hardware boundary",
            "DIP searched multiple simulated capability dimensions. This estimate is intentionally not presented as real-device proof or a certified minimum specification.",
            EvidenceConfidence.Medium,$"artifacts/hardware/{command.ProjectId:N}/{Path.GetFileName(artifactRoot)}",json,_clock.UtcNow,_clock.UtcNow),ct);
        await _uow.SaveChangesAsync(ct);
        await _truth.RecalculateAsync(command.ProjectId,ct);
        return result;
    }

    private static HardwareProfileDto Map(HardwareProfile p)=>new(p.Id,p.Name,p.Platform,p.Source.ToString(),p.CpuSingleScore,p.CpuMultiScore,p.GpuScore,p.MemoryScore,p.StorageScore,p.ThermalScore,p.MemoryBudgetMb,p.TargetFps,p.MetadataJson);
}
