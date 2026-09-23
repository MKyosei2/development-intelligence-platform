// Purpose: Verifies the split post-run processor fails closed before persistence when commit evidence is not a valid SHA.
using System.Reflection;
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Entities;
using FluentAssertions;

namespace Dip.Application.Tests.QualityAutomation;

public sealed class QaPostRunProcessorTests
{
    [Fact]
    public async Task MaterializePipelineAndIssuesAsync_InvalidCommitSha_ReturnsNullBeforePersistence()
    {
        var workspace = Directory.CreateTempSubdirectory("dip-qa-postrun-").FullName;
        await using var plan = QaSplitTestFixtures.CreatePlan(
            workspace,
            commitSha: "not-a-commit-sha");
        var run = QaSplitTestFixtures.CreateRun(
            workspace,
            commitSha: "not-a-commit-sha");
        var sut = new QaPostRunProcessor(
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!);

        try
        {
            var method = typeof(QaPostRunProcessor).GetMethod(
                "MaterializePipelineAndIssuesAsync",
                BindingFlags.Instance | BindingFlags.NonPublic);
            method.Should().NotBeNull();

            var task = (Task<Guid?>)method!.Invoke(
                sut,
                [
                    plan.Command,
                    run,
                    Array.Empty<QaFinding>(),
                    CancellationToken.None
                ])!;

            var pipelineRunId = await task;

            pipelineRunId.Should().BeNull();
        }
        finally
        {
            Directory.Delete(workspace, true);
        }
    }
}
