// Purpose: Implements Project Resource Authorization responsibilities for authorization.
namespace Dip.Application.Features.Authorization;

public enum ProjectResourceKind { QaRun, Incident, DurableJob, BenchmarkRun, ReplaySession, ReplayRun, Artifact, PipelineRun }

/// <summary>Resolves an indirect resource identifier to its owning project before a controller action executes.</summary>
public interface IProjectResourceProjectResolver
{
    Task<Guid?> ResolveProjectIdAsync(ProjectResourceKind kind, Guid resourceId, CancellationToken ct);
}
