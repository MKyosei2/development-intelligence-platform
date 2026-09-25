// Purpose: Verifies generative-QA output is grounded, bounded, and safe before verification.
using Dip.Application.Features.QualityAutomation;
using Dip.Infrastructure.P2Intelligence;
using FluentAssertions;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.Tests.P2Intelligence;

public sealed class GenerativeQaResponseParserTests
{
    private readonly GenerativeQaResponseParser _sut = new(
        Options.Create(new GenerativeQaOptions { MaxPatchFiles = 10, MaxPatchChangedLines = 100 }));

    [Fact]
    public void Hypothesis_rejects_invented_evidence_id()
    {
        var allowed = Guid.NewGuid();
        var invented = Guid.NewGuid();
        var json = $$"""
{"hypotheses":[{"id":"AI-01","title":"Cause","explanation":"Reason","confidence":0.8,"evidenceIds":["{{invented}}"],"suggestedExperiments":["Reproduce"]}]}
""";

        var result = _sut.ParseHypotheses(
            json,
            new HashSet<Guid> { allowed },
            "test",
            "model",
            new AiExecutionMetadata("test", "model", "v1"));

        result.Should().BeNull();
    }

    [Fact]
    public void Fix_accepts_grounded_small_patch_with_regression_test()
    {
        var evidence = Guid.NewGuid();
        var json = $$"""
{
  "summary":"Guard the nullable value.",
  "unifiedDiff":"diff --git a/src/A.cs b/src/A.cs\n--- a/src/A.cs\n+++ b/src/A.cs\n@@ -1 +1 @@\n-return value.Name;\n+return value?.Name ?? string.Empty;",
  "regressionTests":[{"relativePath":"tests/A.Tests.cs","content":"// regression","purpose":"Covers null input"}],
  "evidenceIds":["{{evidence}}"]
}
""";

        var result = _sut.ParseFix(
            json,
            new HashSet<Guid> { evidence },
            "test",
            "model",
            new AiExecutionMetadata("test", "model", "v1"));

        result.Should().NotBeNull();
        result!.EvidenceIds.Should().ContainSingle().Which.Should().Be(evidence);
        result.RegressionTests.Should().ContainSingle();
    }

    [Fact]
    public void Fix_rejects_path_traversal_regression_test()
    {
        var evidence = Guid.NewGuid();
        var json = $$"""
{
  "summary":"candidate",
  "unifiedDiff":"diff --git a/src/A.cs b/src/A.cs\n--- a/src/A.cs\n+++ b/src/A.cs\n@@ -1 +1 @@\n-a\n+b",
  "regressionTests":[{"relativePath":"../outside.cs","content":"x","purpose":"bad"}],
  "evidenceIds":["{{evidence}}"]
}
""";

        var result = _sut.ParseFix(
            json,
            new HashSet<Guid> { evidence },
            "test",
            "model",
            new AiExecutionMetadata("test", "model", "v1"));

        result.Should().BeNull();
    }
}
