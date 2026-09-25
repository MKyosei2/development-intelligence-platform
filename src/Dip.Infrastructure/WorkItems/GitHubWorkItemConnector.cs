// Purpose: Implements GitHub Work Item Connector infrastructure responsibilities for work items.
using Dip.Application.Abstractions.WorkItems;
using Dip.Infrastructure.GitHub;
using Microsoft.Extensions.Options;
using Octokit;

namespace Dip.Infrastructure.WorkItems;

public sealed class GitHubWorkItemConnector : IExternalWorkItemConnector
{
    private readonly GitHubOptions _options; public string ProviderName => "GitHub";
    public GitHubWorkItemConnector(IOptions<GitHubOptions> options) { _options = options.Value; }
    public async Task<CreatedWorkItem> CreateAsync(WorkItemDraft draft, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_options.Token)) throw new InvalidOperationException("GitHub:Token is required to create an external work item.");
        var repo = (draft.Repository ?? string.Empty).Trim('/');
        var parts = repo.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 2) throw new ArgumentException("Repository must be in owner/name form.", nameof(draft));
        var client = new GitHubClient(new ProductHeaderValue(_options.ProductHeader)) { Credentials = new Credentials(_options.Token) };
        ct.ThrowIfCancellationRequested();
        var issue = await client.Issue.Create(parts[0], parts[1], new NewIssue(draft.Title) { Body = draft.Body });
        ct.ThrowIfCancellationRequested();
        return new(issue.Number.ToString(System.Globalization.CultureInfo.InvariantCulture), issue.HtmlUrl, draft.Title);
    }
}
