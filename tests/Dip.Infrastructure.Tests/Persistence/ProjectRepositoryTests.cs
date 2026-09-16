// Purpose: Verifies P ro je ct Re po si to ry Te st s behavior.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.Persistence;
using Dip.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Tests.Persistence;

public sealed class ProjectRepositoryTests
{
    [Fact]
    public async Task AddAndGet_RoundTripsProject()
    {
        var options = new DbContextOptionsBuilder<DipDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        await using var db = new DipDbContext(options);
        var repository = new ProjectRepository(db);

        var project = new Project(
            Guid.NewGuid(),
            "Aurora",
            "https://github.com/example/aurora",
            "develop",
            DateTimeOffset.UtcNow);

        await repository.AddAsync(project, CancellationToken.None);
        await db.SaveChangesAsync();

        var loaded = await repository.GetByIdAsync(project.Id, CancellationToken.None);
        loaded.Should().NotBeNull();
        loaded!.Name.Should().Be("Aurora");
    }

    [Fact]
    public async Task PipelineEvidenceQueries_LinkPrRevisionsWithoutLatestWindowTruncation()
    {
        var options = new DbContextOptionsBuilder<DipDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new DipDbContext(options);
        var projectId = Guid.NewGuid();
        var target = new PipelineRun(
            Guid.NewGuid(),
            projectId,
            new CommitSha("abcdef1234567890"),
            "target-run",
            1,
            "main",
            "github-actions",
            "1",
            DateTimeOffset.UtcNow.AddDays(-30));
        db.PipelineRuns.Add(target);
        for (var i = 0; i < 520; i++)
        {
            db.PipelineRuns.Add(new PipelineRun(
                Guid.NewGuid(),
                projectId,
                new CommitSha($"{i + 1:x7}"),
                $"noise-{i}",
                1,
                "main",
                "collector",
                "1",
                DateTimeOffset.UtcNow.AddMinutes(i)));
        }

        var issue = new QualityIssue(
            Guid.NewGuid(),
            projectId,
            target.Id,
            "fingerprint-target",
            "tests",
            Severity.Critical,
            "Target QA issue",
            "Linked to the target PR revision.",
            null,
            "rule-1",
            null,
            DateTimeOffset.UtcNow);
        db.QualityIssues.Add(issue);
        var evidence = new EvidenceRecord(
            Guid.NewGuid(),
            projectId,
            target.Id,
            "pr:42",
            EvidenceSourceType.Test,
            "integration-test",
            EvidenceOutcome.Failed,
            "Target evidence",
            "Evidence linked through PipelineRunId.",
            EvidenceConfidence.Verified,
            null,
            null,
            DateTimeOffset.UtcNow,
            DateTimeOffset.UtcNow);
        db.EvidenceRecords.Add(evidence);
        await db.SaveChangesAsync();

        var runs = await new PipelineRunRepository(db).ListByCommitShasAsync(
            projectId,
            new[] { "ABCDEF1234567890" },
            CancellationToken.None);
        var issues = await new QualityIssueRepository(db).ListByPipelineRunsAsync(
            projectId,
            runs.Select(x => x.Id).ToArray(),
            CancellationToken.None);
        var evidenceRecords = await new EvidenceRepository(db).ListByPipelineRunsAsync(
            projectId,
            runs.Select(x => x.Id).ToArray(),
            CancellationToken.None);

        runs.Should().ContainSingle(x => x.Id == target.Id);
        issues.Should().ContainSingle(x => x.Id == issue.Id);
        evidenceRecords.Should().ContainSingle(x => x.Id == evidence.Id);
    }

    [Fact]
    public async Task AuditSearchPage_UsesStableCursorWithoutDuplicateRows()
    {
        var options = new DbContextOptionsBuilder<DipDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var db = new DipDbContext(options);
        var projectId = Guid.NewGuid();
        var baseTime = new DateTimeOffset(2026, 8, 22, 0, 0, 0, TimeSpan.Zero);
        for (var i = 0; i < 205; i++)
        {
            db.AuditLogs.Add(new AuditLog(
                Guid.NewGuid(),
                projectId,
                "notification",
                "notification.viewed",
                "test",
                "alert-1",
                "{}",
                baseTime,
                reason: $"event-{i}"));
        }
        await db.SaveChangesAsync();

        var repository = new AuditLogRepository(db);
        var first = await repository.SearchPageAsync(
            projectId, "alert-1", "notification", null, null, null, null, null, 100, CancellationToken.None);
        var second = await repository.SearchPageAsync(
            projectId, "alert-1", "notification", null, null, null, first.NextBeforeUtc, first.NextBeforeId, 100, CancellationToken.None);

        first.Items.Should().HaveCount(100);
        second.Items.Should().HaveCount(100);
        first.NextBeforeUtc.Should().NotBeNull();
        first.NextBeforeId.Should().NotBeNull();
        first.Items.Select(x => x.Id).Intersect(second.Items.Select(x => x.Id)).Should().BeEmpty();
    }
}
