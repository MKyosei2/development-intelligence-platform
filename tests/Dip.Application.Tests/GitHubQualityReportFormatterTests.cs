// Purpose: Defines GitHub Quality Report Formatter Tests responsibilities for this repository area.
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
