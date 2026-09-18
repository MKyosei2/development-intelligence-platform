// Purpose: Verifies behavior and invariants covered by G it Hu bQ ua li ty Re po rt Fo rm at te rT es ts.
using Dip.Application.Features.GitHub;
using Xunit;

namespace Dip.Application.Tests;

public sealed class GitHubQualityReportFormatterTests
{
    [Fact]
    public void Format_ContainsGateAndScore()
    {
        var result = GitHubQualityReportFormatter.Format(
            "Failed",
            42,
            new[] { "Critical issue detected." });

        Assert.Contains("Failed", result);
        Assert.Contains("42/100", result);
        Assert.Contains("Critical issue detected.", result);
    }

    [Fact]
    public void Format_RemovesHtmlCommentTokens()
    {
        var result = GitHubQualityReportFormatter.Format(
            "Warning",
            80,
            new[] { "<!-- injected -->" });

        Assert.DoesNotContain("<!--", result);
        Assert.DoesNotContain("-->", result);
    }
}
