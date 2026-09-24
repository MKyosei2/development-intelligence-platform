// Purpose: Verifies Generic JSON report validation boundaries for revisions, repositories, timestamps, metrics, evidence, and severity.
using System.Text;
using GenericJsonCollector.Models;
using GenericJsonCollector.Validation;

namespace GenericJsonCollector.Tests;

public sealed class CollectorReportValidatorTests
{
    private readonly CollectorReportValidator _validator = new();

    [Theory]
    [InlineData("abcdef1")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef")]
    public void Validate_AcceptsShaBoundaries(string sha)
    {
        var errors = _validator.Validate(CollectorReportTestData.CreateValid() with { CommitSha = sha });
        Assert.DoesNotContain(errors, error => error.Contains("CommitSha", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("abcdef")]
    [InlineData("0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef0")]
    [InlineData("abcdefg")]
    public void Validate_RejectsInvalidSha(string sha)
    {
        var errors = _validator.Validate(CollectorReportTestData.CreateValid() with { CommitSha = sha });
        Assert.Contains(errors, error => error.Contains("CommitSha", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("openai/example")]
    [InlineData("https://github.com/openai/example")]
    [InlineData("http://git.example.local/team/repo")]
    public void Validate_AcceptsSupportedRepositoryForms(string repository)
    {
        var errors = _validator.Validate(CollectorReportTestData.CreateValid() with { Repository = repository });
        Assert.DoesNotContain(errors, error => error.Contains("Repository must", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("file:///tmp/repo")]
    [InlineData("owner/group/repository")]
    [InlineData("owner-only")]
    public void Validate_RejectsUnsupportedRepositoryForms(string repository)
    {
        var errors = _validator.Validate(CollectorReportTestData.CreateValid() with { Repository = repository });
        Assert.Contains(errors, error => error.Contains("Repository must", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsReversedTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        var report = CollectorReportTestData.CreateValid() with
        {
            StartedAt = now,
            FinishedAt = now.AddMinutes(-1)
        };

        var errors = _validator.Validate(report);
        Assert.Contains(errors, error => error.Contains("must not precede", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsTimestampsBeyondFutureBoundary()
    {
        var future = DateTimeOffset.UtcNow.AddHours(25);
        var report = CollectorReportTestData.CreateValid() with
        {
            StartedAt = future,
            FinishedAt = future.AddMinutes(1)
        };

        var errors = _validator.Validate(report);
        Assert.Contains(errors, error => error.Contains("24 hours", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsDuplicateMetricIdentityCaseInsensitively()
    {
        var metrics = new[]
        {
            new CollectorMetric("frame_time", 10m, "ms", null, "env", "scene", "windows", "release"),
            new CollectorMetric("FRAME_TIME", 11m, "ms", null, "ENV", "SCENE", "WINDOWS", "RELEASE")
        };

        var errors = _validator.Validate(CollectorReportTestData.CreateValid(metrics: metrics));
        Assert.Contains(errors, error => error.Contains("Duplicate metric key", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AcceptsEvidenceBelowUtf8ByteLimit()
    {
        var evidence = JsonPayload(256 * 1024 - 64);
        Assert.True(Encoding.UTF8.GetByteCount(evidence) < 256 * 1024);
        var issue = ValidIssue(evidenceJson: evidence);

        var errors = _validator.Validate(CollectorReportTestData.CreateValid(issues: new[] { issue }));
        Assert.DoesNotContain(errors, error => error.Contains("EvidenceJson exceeds", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsEvidenceAboveUtf8ByteLimit()
    {
        var evidence = JsonPayload(256 * 1024 + 16);
        Assert.True(Encoding.UTF8.GetByteCount(evidence) > 256 * 1024);
        var issue = ValidIssue(evidenceJson: evidence);

        var errors = _validator.Validate(CollectorReportTestData.CreateValid(issues: new[] { issue }));
        Assert.Contains(errors, error => error.Contains("EvidenceJson exceeds", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("Info")]
    [InlineData("warning")]
    [InlineData("CRITICAL")]
    public void Validate_AcceptsDocumentedSeverityValuesCaseInsensitively(string severity)
    {
        var errors = _validator.Validate(
            CollectorReportTestData.CreateValid(issues: new[] { ValidIssue(severity: severity) }));
        Assert.DoesNotContain(errors, error => error.Contains("Severity must", StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RejectsUnknownSeverity()
    {
        var errors = _validator.Validate(
            CollectorReportTestData.CreateValid(issues: new[] { ValidIssue(severity: "Error") }));
        Assert.Contains(errors, error => error.Contains("Severity must", StringComparison.Ordinal));
    }

    private static CollectorIssue ValidIssue(
        string severity = "Warning",
        string? evidenceJson = null)
        => new(
            Category: "asset",
            Severity: severity,
            Title: "Example finding",
            Description: "Example description",
            Source: "asset.fbx",
            RuleId: "example-rule",
            EvidenceJson: evidenceJson);

    private static string JsonPayload(int characters)
        => "{\"payload\":\"" + new string('x', characters) + "\"}";
}
