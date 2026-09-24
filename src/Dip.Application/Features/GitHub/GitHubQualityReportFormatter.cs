// Purpose: Implements Git Hub Quality Report Formatter responsibilities for git hub.
namespace Dip.Application.Features.GitHub;

public static class GitHubQualityReportFormatter
{
    public static string Format(
        string gateStatus,
        int healthScore,
        IReadOnlyCollection<string> reasons)
    {
        var lines = new List<string>
        {
            "## Development Intelligence Report",
            string.Empty,
            $"**Quality Gate:** {gateStatus}",
            $"**Health Score:** {healthScore}/100",
            string.Empty,
            "### Findings"
        };

        if (reasons.Count == 0)
        {
            lines.Add("- No blocking findings.");
        }
        else
        {
            lines.AddRange(reasons.Select(x => $"- {Sanitize(x)}"));
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string Sanitize(string value) =>
        value.Replace("<!--", string.Empty, StringComparison.Ordinal)
             .Replace("-->", string.Empty, StringComparison.Ordinal)
             .Trim();
}
