// Purpose: Coordinates P2 Ab st ra ct io ns application behavior for Intelligence.
using Dip.Domain.Entities;
namespace Dip.Application.Features.Intelligence;
public interface ICommunityHardwareStore{Task AddAsync(CommunityHardwareSample sample,CancellationToken ct);Task<IReadOnlyList<CommunityHardwareSample>> ListAsync(string hardwareKey,CancellationToken ct);}
public interface IIntelligenceRunStore{Task AddAsync(IntelligenceRun run,CancellationToken ct);Task<IntelligenceRun?> GetAsync(Guid id,CancellationToken ct);Task<IReadOnlyList<IntelligenceRun>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);}
public interface IProjectForecastStore{Task AddAsync(ProjectForecast forecast,CancellationToken ct);Task<IReadOnlyList<ProjectForecast>> ListRecentAsync(Guid projectId,int take,CancellationToken ct);}
public interface IMinimumHardwareRunner{Task<MinimumHardwareResult> FindAsync(MinimumHardwareRequest request,string artifactRoot,CancellationToken ct);}
public interface IRegressionRunner{Task<RegressionResult> FindOriginAsync(RegressionRequest request,string artifactRoot,CancellationToken ct);}
public interface IReliabilityRunner{Task<ReliabilityResult> RunAsync(ReliabilityRequest request,string artifactRoot,CancellationToken ct);}
public interface IMediaRegressionRunner{Task<MediaRegressionResult> CompareAsync(VisualRegressionRequest request,string artifactRoot,string kind,CancellationToken ct);}
