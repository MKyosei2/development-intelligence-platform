// Purpose: Verifies cross-project benchmark comparison uses equivalent definitions and latest successful project measurements.
using Dip.Application.Abstractions.Persistence;
using Dip.Application.Features.QualityGovernance;
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using FluentAssertions;

namespace Dip.Application.Tests.QualityGovernance;

public sealed class QaBenchmarkComparisonServiceTests
{
    [Fact]
    public async Task CompareProjectsAsync_ReturnsLatestComparableMeasurementsOrderedFromBest()
    {
        var now = new DateTimeOffset(2026, 9, 23, 5, 0, 0, TimeSpan.Zero);
        var definition = new BenchmarkDefinition(
            Guid.NewGuid(),
            "frame-p95",
            "Frame P95",
            "runtime",
            "{}",
            true,
            now);
        var firstProject = new Project(
            Guid.NewGuid(),
            "First Project",
            "https://github.com/example/first",
            "main",
            now);
        var secondProject = new Project(
            Guid.NewGuid(),
            "Second Project",
            "https://github.com/example/second",
            "main",
            now);

        var firstRun = SuccessfulRun(firstProject.Id, definition.Id, "aaaaaaa", now.AddMinutes(-2));
        var secondRun = SuccessfulRun(secondProject.Id, definition.Id, "bbbbbbb", now.AddMinutes(-1));

        var benchmarks = new FakeBenchmarkRepository(definition)
        {
            Runs = [firstRun, secondRun]
        };
        benchmarks.Metrics[firstRun.Id] =
        [
            new BenchmarkMetric(Guid.NewGuid(), firstRun.Id, "frame_time", 10m, "ms", "p95")
        ];
        benchmarks.Metrics[secondRun.Id] =
        [
            new BenchmarkMetric(Guid.NewGuid(), secondRun.Id, "frame_time", 12m, "ms", "p95")
        ];

        var projects = new FakeProjectRepository([firstProject, secondProject]);
        var sut = new QaBenchmarkComparisonService(benchmarks, projects);

        var result = await sut.CompareProjectsAsync(
            "frame-p95",
            "frame_time",
            "p95",
            20,
            CancellationToken.None);

        result.DefinitionKey.Should().Be("frame-p95");
        result.Projects.Should().HaveCount(2);
        result.Projects[0].ProjectName.Should().Be("First Project");
        result.Projects[0].DifferenceFromBestPercent.Should().Be(0m);
        result.Projects[1].DifferenceFromBestPercent.Should().Be(20m);
        result.Caution.Should().Contain("descriptive");
    }

    private static BenchmarkRun SuccessfulRun(
        Guid projectId,
        Guid definitionId,
        string commit,
        DateTimeOffset at)
    {
        var run = new BenchmarkRun(
            Guid.NewGuid(),
            projectId,
            definitionId,
            Guid.NewGuid(),
            "benchmark",
            commit,
            "env",
            "config",
            at.AddMinutes(-1));
        run.Start(at);
        run.Complete(BenchmarkRunStatus.Succeeded, "ok", at.AddSeconds(10));
        return run;
    }

    private sealed class FakeBenchmarkRepository(BenchmarkDefinition definition)
        : IBenchmarkRepository
    {
        public IReadOnlyList<BenchmarkRun> Runs { get; set; } = [];
        public Dictionary<Guid, IReadOnlyList<BenchmarkMetric>> Metrics { get; } = [];

        public Task AddDefinitionAsync(BenchmarkDefinition value, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<BenchmarkDefinition?> GetDefinitionAsync(Guid id, CancellationToken ct) =>
            Task.FromResult<BenchmarkDefinition?>(id == definition.Id ? definition : null);

        public Task<BenchmarkDefinition?> GetDefinitionByKeyAsync(
            string key,
            CancellationToken ct) =>
            Task.FromResult<BenchmarkDefinition?>(
                key.Equals(definition.Key, StringComparison.OrdinalIgnoreCase)
                    ? definition
                    : null);

        public Task<IReadOnlyList<BenchmarkDefinition>> ListDefinitionsAsync(
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<BenchmarkDefinition>>([definition]);

        public Task AddRunAsync(BenchmarkRun run, CancellationToken ct) =>
            Task.CompletedTask;

        public Task<BenchmarkRun?> GetRunAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(Runs.FirstOrDefault(x => x.Id == id));

        public Task<BenchmarkRun?> FindBaselineAsync(
            Guid projectId,
            Guid definitionId,
            string environmentFingerprint,
            string configurationHash,
            string excludingCommit,
            CancellationToken ct) =>
            Task.FromResult<BenchmarkRun?>(null);

        public Task<IReadOnlyList<BenchmarkRun>> ListSucceededRunsAsync(
            Guid definitionId,
            int take,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<BenchmarkRun>>(Runs.Take(take).ToArray());

        public Task AddMetricsAsync(
            IEnumerable<BenchmarkMetric> metrics,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<BenchmarkMetric>> ListMetricsAsync(
            Guid runId,
            CancellationToken ct) =>
            Task.FromResult(
                Metrics.TryGetValue(runId, out var values)
                    ? values
                    : (IReadOnlyList<BenchmarkMetric>)Array.Empty<BenchmarkMetric>());

        public Task AddComparisonsAsync(
            IEnumerable<BenchmarkComparison> comparisons,
            CancellationToken ct) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<BenchmarkComparison>> ListComparisonsAsync(
            Guid runId,
            CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<BenchmarkComparison>>([]);
    }

    private sealed class FakeProjectRepository(IReadOnlyList<Project> projects)
        : IProjectRepository
    {
        public Task<Project?> GetByIdAsync(Guid id, CancellationToken ct) =>
            Task.FromResult(projects.FirstOrDefault(x => x.Id == id));

        public Task<IReadOnlyList<Project>> ListAsync(CancellationToken ct) =>
            Task.FromResult(projects);

        public Task AddAsync(Project project, CancellationToken ct) =>
            Task.CompletedTask;
    }
}
