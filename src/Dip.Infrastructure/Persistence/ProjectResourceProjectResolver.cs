// Purpose: Implements Project Resource Project Resolver infrastructure behavior for Persistence.
using Dip.Application.Features.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence;

public sealed class ProjectResourceProjectResolver(DipDbContext db) : IProjectResourceProjectResolver
{
    public Task<Guid?> ResolveProjectIdAsync(ProjectResourceKind kind, Guid id, CancellationToken ct) => kind switch
    {
        ProjectResourceKind.QaRun => db.QaRuns.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ProjectResourceKind.Incident => db.Incidents.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ProjectResourceKind.DurableJob => db.DurableJobs.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ProjectResourceKind.BenchmarkRun => db.BenchmarkRuns.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ProjectResourceKind.ReplaySession => db.GameReplaySessions.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ProjectResourceKind.ReplayRun => db.GameReplayRuns.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ProjectResourceKind.Artifact => db.ArtifactRecords.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        ProjectResourceKind.PipelineRun => db.PipelineRuns.AsNoTracking().Where(x => x.Id == id).Select(x => (Guid?)x.ProjectId).SingleOrDefaultAsync(ct),
        _ => Task.FromResult<Guid?>(null)
    };
}
