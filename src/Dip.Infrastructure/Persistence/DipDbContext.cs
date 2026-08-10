using Dip.Application.Abstractions.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Persistence;

public sealed class DipDbContext : DbContext, IUnitOfWork
{
    public DipDbContext(DbContextOptions<DipDbContext> options) : base(options) { }
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<PipelineRun> PipelineRuns => Set<PipelineRun>();
    public DbSet<Metric> Metrics => Set<Metric>();
    public DbSet<QualityIssue> QualityIssues => Set<QualityIssue>();
    public DbSet<QualityGateResult> QualityGateResults => Set<QualityGateResult>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedWebhook> ProcessedWebhooks => Set<ProcessedWebhook>();
    public DbSet<EvidenceRecord> EvidenceRecords => Set<EvidenceRecord>();
    public DbSet<ProjectClaim> ProjectClaims => Set<ProjectClaim>();
    public DbSet<ClaimEvidenceLink> ClaimEvidenceLinks => Set<ClaimEvidenceLink>();
    public DbSet<ProjectTruthSnapshot> ProjectTruthSnapshots => Set<ProjectTruthSnapshot>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DipDbContext).Assembly);
    }
}
