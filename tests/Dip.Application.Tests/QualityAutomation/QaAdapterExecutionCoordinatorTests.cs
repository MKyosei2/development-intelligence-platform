// Purpose: Verifies adapter, cross-cutting, and media-regression execution preserves failures, findings, and cancellation semantics.
using Dip.Application.Features.P2Intelligence;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaAdapterExecutionCoordinatorTests
{
    [Fact]
    public async Task ExecuteAsync_WithNoopAdapter_ProducesOneSuccessfulOutput()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-adapter-").FullName;
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var sut = new QaAdapterExecutionCoordinator(new FakeMediaRunner(), Array.Empty<ICrossCuttingQaPack>());

            var result = await sut.ExecuteAsync(plan, CancellationToken.None);

            result.Outputs.Should().ContainSingle();
            result.Outputs[0].Result.Findings.Should().BeEmpty();
            result.AdapterFailures.Should().BeEmpty();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AdapterException_IsMaterializedWithoutAbortingOtherEvidence()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-adapter-fail-").FullName;
        try
        {
            var selection = new QaAdapterSelection(new LanguageId("csharp"), new ThrowingAdapter());
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace, selections: [selection]);
            var sut = new QaAdapterExecutionCoordinator(new FakeMediaRunner(), []);

            var result = await sut.ExecuteAsync(plan, CancellationToken.None);

            result.Outputs.Should().ContainSingle();
            result.Outputs[0].Result.Tools.Should().ContainSingle();
            result.Outputs[0].Result.Tools[0].WasAvailable.Should().BeFalse();
            result.Outputs[0].Result.SummaryJson.Should().Contain("adapter boom");
            result.AdapterFailures.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AdapterCancellation_IsNotConvertedIntoFailureEvidence()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-adapter-cancel-").FullName;
        try
        {
            var selection = new QaAdapterSelection(new LanguageId("csharp"), new CancellingAdapter());
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace, selections: [selection]);
            var sut = new QaAdapterExecutionCoordinator(new FakeMediaRunner(), []);

            Func<Task> act = () => sut.ExecuteAsync(plan, new CancellationToken(canceled: true));

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_CrossCuttingPacks_RecordSuccessAndFailure()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-cross-").FullName;
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            ICrossCuttingQaPack[] packs =
            [
                new SuccessfulPack(),
                new ThrowingPack(),
                new NotApplicablePack()
            ];
            var sut = new QaAdapterExecutionCoordinator(new FakeMediaRunner(), packs);

            var result = await sut.ExecuteAsync(plan, CancellationToken.None);

            result.CrossResults.Should().HaveCount(2);
            result.CrossResults.Should().Contain(x => x.PackId == "success-pack");
            result.CrossResults.Should().Contain(x => x.PackId == "throw-pack");
            result.CrossResults.Single(x => x.PackId == "throw-pack").Tools[0].WasAvailable.Should().BeFalse();
            result.AdapterFailures.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Theory]
    [InlineData("Warning", "Warning")]
    [InlineData("Failed", "Critical")]
    public async Task ExecuteAsync_MediaRegressionWarningOrFailure_ProducesFinding(
        string status,
        string expectedSeverity)
    {
        var workspace = CreateMediaWorkspace("Visual");
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var runner = new FakeMediaRunner(status);
            var sut = new QaAdapterExecutionCoordinator(runner, []);

            var result = await sut.ExecuteAsync(plan, CancellationToken.None);

            var media = result.CrossResults.Single(x => x.PackId == "media-regression-visual");
            media.Findings.Should().ContainSingle();
            media.Findings[0].Severity.ToString().Should().Be(expectedSeverity);
            media.Tools[0].WasAvailable.Should().BeTrue();
            result.AdapterFailures.Should().BeEmpty();
            runner.Calls.Should().Be(1);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_UnsupportedMediaType_IsWarningConfigurationFinding()
    {
        var workspace = CreateMediaWorkspace("UnsupportedType");
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var runner = new FakeMediaRunner();
            var sut = new QaAdapterExecutionCoordinator(runner, []);

            var result = await sut.ExecuteAsync(plan, CancellationToken.None);

            var config = result.CrossResults.Single(x => x.PackId == "media-regression-config");
            config.Findings.Should().ContainSingle();
            config.Findings[0].Severity.Should().Be(Severity.Warning);
            config.Findings[0].RuleId.Should().Be("DIP-MEDIA-TYPE");
            runner.Calls.Should().Be(0);
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MediaInfrastructureFailure_IsPreservedAsAdapterFailure()
    {
        var workspace = CreateMediaWorkspace("Audio");
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var sut = new QaAdapterExecutionCoordinator(new FakeMediaRunner("InfrastructureFailure"), []);

            var result = await sut.ExecuteAsync(plan, CancellationToken.None);

            var media = result.CrossResults.Single(x => x.PackId == "media-regression-audio");
            media.Tools[0].WasAvailable.Should().BeFalse();
            media.Tools[0].Succeeded.Should().BeFalse();
            media.Findings.Should().BeEmpty();
            result.AdapterFailures.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MediaRunnerException_IsMaterialized()
    {
        var workspace = CreateMediaWorkspace("Video");
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var sut = new QaAdapterExecutionCoordinator(new ThrowingMediaRunner(), []);

            var result = await sut.ExecuteAsync(plan, CancellationToken.None);

            var media = result.CrossResults.Single(x => x.PackId == "media-regression");
            media.Tools.Should().ContainSingle();
            media.Tools[0].WasAvailable.Should().BeFalse();
            media.SummaryJson.Should().Contain("media boom");
            result.AdapterFailures.Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_MediaCancellation_Propagates()
    {
        var workspace = CreateMediaWorkspace("Visual");
        try
        {
            await using var plan = QaSplitTestFixtures.CreatePlan(workspace);
            var sut = new QaAdapterExecutionCoordinator(new CancellingMediaRunner(), []);

            Func<Task> act = () => sut.ExecuteAsync(plan, new CancellationToken(canceled: true));

            await act.Should().ThrowAsync<OperationCanceledException>();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }

    private static string CreateMediaWorkspace(string type)
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-media-").FullName;
        var dip = Directory.CreateDirectory(Path.Combine(workspace, ".dip")).FullName;
        File.WriteAllText(
            Path.Combine(dip, "production.json"),
            $$"""
            {
              "mediaRegression": [
                {
                  "type": "{{type}}",
                  "baselinePath": "baseline.png",
                  "currentPath": "current.png",
                  "warningThresholdPercent": 2,
                  "failureThresholdPercent": 10,
                  "videoSampleFps": 2
                }
              ]
            }
            """);
        return workspace;
    }

    private sealed class ThrowingAdapter : IQaAdapter
    {
        public string AdapterId => "throwing";
        public int Priority => 1;
        public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace) => true;

        public Task<QaAdapterResult> RunAsync(
            QaExecutionCommand command,
            string workspace,
            string artifactRoot,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("adapter boom");
    }

    private sealed class CancellingAdapter : IQaAdapter
    {
        public string AdapterId => "cancel";
        public int Priority => 1;
        public bool Supports(LanguageId language, QaTechnologyProfile profile, string workspace) => true;

        public Task<QaAdapterResult> RunAsync(
            QaExecutionCommand command,
            string workspace,
            string artifactRoot,
            CancellationToken cancellationToken) =>
            Task.FromCanceled<QaAdapterResult>(new CancellationToken(canceled: true));
    }

    private sealed class SuccessfulPack : ICrossCuttingQaPack
    {
        public string PackId => "success-pack";
        public bool Applies(QaTechnologyProfile profile, string workspace) => true;

        public Task<QaCrossCuttingResult> RunAsync(
            QaExecutionCommand command,
            QaTechnologyProfile profile,
            string workspace,
            string artifactRoot,
            CancellationToken ct) =>
            Task.FromResult(new QaCrossCuttingResult(PackId, [], [], "{}"));
    }

    private sealed class ThrowingPack : ICrossCuttingQaPack
    {
        public string PackId => "throw-pack";
        public bool Applies(QaTechnologyProfile profile, string workspace) => true;

        public Task<QaCrossCuttingResult> RunAsync(
            QaExecutionCommand command,
            QaTechnologyProfile profile,
            string workspace,
            string artifactRoot,
            CancellationToken ct) =>
            throw new InvalidOperationException("pack boom");
    }

    private sealed class NotApplicablePack : ICrossCuttingQaPack
    {
        public string PackId => "skip-pack";
        public bool Applies(QaTechnologyProfile profile, string workspace) => false;

        public Task<QaCrossCuttingResult> RunAsync(
            QaExecutionCommand command,
            QaTechnologyProfile profile,
            string workspace,
            string artifactRoot,
            CancellationToken ct) =>
            throw new InvalidOperationException("must not execute");
    }

    private sealed class FakeMediaRunner(string status = "Passed") : IMediaRegressionRunner
    {
        public int Calls { get; private set; }

        public Task<MediaRegressionResult> AnalyzeAsync(
            MediaRegressionCommand command,
            CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new MediaRegressionResult(
                status,
                command.Type,
                status == "Passed" ? 0m : 12.5m,
                99m,
                null,
                $"media {status}",
                "artifact",
                "{}"));
        }
    }

    private sealed class ThrowingMediaRunner : IMediaRegressionRunner
    {
        public Task<MediaRegressionResult> AnalyzeAsync(
            MediaRegressionCommand command,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("media boom");
    }

    private sealed class CancellingMediaRunner : IMediaRegressionRunner
    {
        public Task<MediaRegressionResult> AnalyzeAsync(
            MediaRegressionCommand command,
            CancellationToken cancellationToken) =>
            Task.FromCanceled<MediaRegressionResult>(new CancellationToken(canceled: true));
    }
}
