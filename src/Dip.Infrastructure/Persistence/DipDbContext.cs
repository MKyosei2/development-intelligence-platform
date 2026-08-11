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
    public DbSet<QaRun> QaRuns => Set<QaRun>();
    public DbSet<QaFinding> QaFindings => Set<QaFinding>();
    public DbSet<ReproductionAttempt> ReproductionAttempts => Set<ReproductionAttempt>();
    public DbSet<HardwareProfile> HardwareProfiles => Set<HardwareProfile>();
    public DbSet<HardwareCalibration> HardwareCalibrations => Set<HardwareCalibration>();
    public DbSet<PerformanceSimulationRun> PerformanceSimulationRuns => Set<PerformanceSimulationRun>();
    public DbSet<P2IntelligenceRecord> P2IntelligenceRecords => Set<P2IntelligenceRecord>();
    public DbSet<HardwareCommunitySample> HardwareCommunitySamples => Set<HardwareCommunitySample>();
    public DbSet<DecisionTrace> DecisionTraces => Set<DecisionTrace>();
    public DbSet<AdaptiveQaPlanSnapshot> AdaptiveQaPlanSnapshots => Set<AdaptiveQaPlanSnapshot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(DipDbContext).Assembly);
    }
}
