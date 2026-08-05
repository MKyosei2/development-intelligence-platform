using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence.Repositories;

/// <summary>
/// PostgreSQL-backed project repository.
///
/// The other repository implementations are intentionally kept in their
/// dedicated files to prevent duplicate class definitions:
/// PipelineRunRepository.cs, MetricRepository.cs,
/// QualityIssueRepository.cs, OutboxRepository.cs,
/// ProcessedWebhookRepository.cs and DashboardReadRepository.cs.
/// </summary>
public sealed class ProjectRepository : IProjectRepository
{
    private readonly DipDbContext _dbContext;

    public ProjectRepository(DipDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Project?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return _dbContext.Projects.SingleOrDefaultAsync(
            project => project.Id == id,
            cancellationToken);
    }

    public async Task<IReadOnlyList<Project>> ListAsync(
        CancellationToken cancellationToken)
    {
        return await _dbContext.Projects
            .AsNoTracking()
            .OrderBy(project => project.Name)
            .ToListAsync(cancellationToken);
    }

    public Task AddAsync(
        Project project,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(project);

        return _dbContext.Projects
            .AddAsync(project, cancellationToken)
            .AsTask();
    }
}
