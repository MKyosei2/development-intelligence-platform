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
    private readonly IPerformanceSimulationRepository _simulations;private readonly IEvidenceRepository _evidence;private readonly IUnitOfWork _uow;private readonly ISystemClock _clock;
    private readonly IHardwareCalibrationRunner _calibrator;private readonly IPerformanceSimulationRunner _runner;private readonly IQaWorkspaceResolver _workspace;private readonly ProjectTruthService _truth;
    public HardwareOrchestrationService(IProjectRepository projects,IHardwareProfileRepository profiles,IHardwareCalibrationRepository calibrations,IPerformanceSimulationRepository simulations,
        IEvidenceRepository evidence,IUnitOfWork uow,ISystemClock clock,IHardwareCalibrationRunner calibrator,IPerformanceSimulationRunner runner,IQaWorkspaceResolver workspace,ProjectTruthService truth)
    {_projects=projects;_profiles=profiles;_calibrations=calibrations;_simulations=simulations;_evidence=evidence;_uow=uow;_clock=clock;_calibrator=calibrator;_runner=runner;_workspace=workspace;_truth=truth;}

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
        var baseline=await _calibrations.GetLatestAsync(command.ProjectId,ct)??throw new InvalidOperationException("Run hardware calibration first.");
        var tested=new List<string>();decimal low=20m,high=100m,best=100m;decimal confidence=55m;
        for(var i=0;i<5;i++)
        {
            var percent=i<4?new[]{100m,80m,60m,40m}[i]:(low+high)/2m;
            var name=$"DIP Boundary {command.ProjectId:N} {percent:0.##}% {Guid.NewGuid():N}";var profile=new HardwareProfile(Guid.NewGuid(),name,"Estimated",HardwareProfileSource.SimulationTarget,
                baseline.CpuSingleScore*percent/100m,baseline.CpuMultiScore*percent/100m,baseline.GpuScore*percent/100m,baseline.MemoryScore*percent/100m,baseline.StorageScore*percent/100m,65m,command.MemoryBudgetMb,command.TargetFps,
                JsonSerializer.Serialize(new{kind="minimum-hardware-search",percent}),_clock.UtcNow);await _profiles.AddAsync(profile,ct);await _uow.SaveChangesAsync(ct);
            var simulation=await SimulateAsync(new(command.ProjectId,profile.Id,command.SubjectKey,command.WorkspaceRelativePath,command.Executable,command.Arguments,command.DurationSeconds,true),ct);
            tested.Add($"{percent:0.##}% => {simulation.Status}");
            if(simulation.Status==SimulationStatus.InfrastructureFailure.ToString()||simulation.Status==SimulationStatus.Unsupported.ToString()||simulation.Status==SimulationStatus.Cancelled.ToString())
                return new(best,best,0m,"InfrastructureFailure",tested,"The boundary search stopped because a simulation could not produce product evidence. No minimum hardware conclusion should be drawn from this run.");
            var pass=simulation.Status==SimulationStatus.Passed.ToString();
            if(pass){best=Math.Min(best,percent);high=percent;}else low=Math.Max(low,percent);
        }
        return new(best,best,confidence,"Estimated",tested,"This is a simulation boundary estimate. It must not be presented as a certified minimum device specification until real-device evidence exists.");
    }

    private static HardwareProfileDto Map(HardwareProfile p)=>new(p.Id,p.Name,p.Platform,p.Source.ToString(),p.CpuSingleScore,p.CpuMultiScore,p.GpuScore,p.MemoryScore,p.StorageScore,p.ThermalScore,p.MemoryBudgetMb,p.TargetFps,p.MetadataJson);
}
