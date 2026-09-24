// Purpose: Discovers GitHub repository resources and probes repository capabilities using an already-authenticated client.
using Dip.Application.Features.Integrations;
using Dip.Domain.Entities;

namespace Dip.Infrastructure.Integrations.GitHub;

internal sealed class GitHubDiscoveryService
{
    internal async Task<IReadOnlyList<ConnectorResourceDto>>
        DiscoverWithClientAsync(
            IntegrationConnection connection,
            HttpClient client,
            ConnectorDiscoveryQuery query,
            CancellationToken cancellationToken)
    {
        var limit = Math.Clamp(query.Limit, 1, 500);
        var output = new List<ConnectorResourceDto>();
        var maxPages = Math.Max(
            1,
            (int)Math.Ceiling(limit / 100d));

        for (var page = 1;
             page <= maxPages && output.Count < limit;
             page++)
        {
            var path = GitHubAuthorizedClientFactory.IsApp(connection.Mode)
                ? $"installation/repositories?per_page=100&page={page}"
                : $"user/repos?per_page=100&page={page}"
                  + "&sort=updated&affiliation=owner,collaborator,organization_member";

            using var response = await client.GetAsync(
                path,
                cancellationToken);

            response.EnsureSuccessStatusCode();

            using var document = await GitHubApiJson.ReadJsonAsync(
                response,
                cancellationToken);

            var array = GitHubAuthorizedClientFactory.IsApp(connection.Mode)
                ? document.RootElement.GetProperty("repositories")
                : document.RootElement;

            var pageCount = 0;

            foreach (var item in array.EnumerateArray())
            {
                pageCount++;
                var resource = MapRepository(item);

                if (!Matches(resource, query))
                    continue;

                output.Add(resource);

                if (output.Count >= limit)
                    break;
            }

            if (pageCount < 100)
                break;
        }

        return output
            .OrderByDescending(item => item.UpdatedAtUtc)
            .ThenBy(
                item => item.FullName,
                StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    internal async Task<IReadOnlyList<ConnectorCapability>>
        GetCapabilitiesWithClientAsync(
            IntegrationConnection connection,
            HttpClient client,
            CancellationToken cancellationToken)
    {
        IReadOnlyList<ConnectorResourceDto> resources;

        try
        {
            resources = await DiscoverWithClientAsync(
                connection,
                client,
                new ConnectorDiscoveryQuery(Limit: 1),
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is HttpRequestException
            or InvalidOperationException)
        {
            return UnavailableCapabilities(
                GitHubApiJson.Safe(exception.Message));
        }

        if (resources.Count == 0)
        {
            return
            [
                new(
                    "repository.metadata",
                    true,
                    "Authentication succeeded; no repository is visible."),
                new(
                    "repository.contents",
                    false,
                    "No repository is available for capability probing."),
                new(
                    "repository.pull_requests",
                    false,
                    "No repository is available for capability probing."),
                new(
                    "repository.actions",
                    false,
                    "No repository is available for capability probing."),
                new(
                    "repository.workspace_sync",
                    false,
                    "No repository is available for capability probing.")
            ];
        }

        var resource = resources[0];
        var owner = resource.FullName.Split('/')[0];
        var repoPath =
            $"repos/{GitHubApiJson.Escape(owner)}/{GitHubApiJson.Escape(resource.Name)}";

        var contents = await IsSuccessAsync(
            client,
            $"{repoPath}/commits/{GitHubApiJson.Escape(resource.DefaultBranch ?? "main")}",
            cancellationToken);

        var pulls = await IsSuccessAsync(
            client,
            $"{repoPath}/pulls?per_page=1",
            cancellationToken);

        var actions = await IsSuccessAsync(
            client,
            $"{repoPath}/actions/runs?per_page=1",
            cancellationToken);

        return
        [
            new("repository.metadata", true),
            new(
                "repository.contents",
                contents,
                contents ? null : "Latest commit endpoint is not readable."),
            new(
                "repository.pull_requests",
                pulls,
                pulls ? null : "Pull-request endpoint is not readable."),
            new(
                "repository.actions",
                actions,
                actions ? null : "Actions endpoint is not readable."),
            new(
                "repository.workspace_sync",
                contents,
                contents
                    ? null
                    : "Workspace sync requires repository content access.")
        ];
    }

    private static IReadOnlyList<ConnectorCapability>
        UnavailableCapabilities(string detail) =>
        [
            new("repository.metadata", false, detail),
            new(
                "repository.contents",
                false,
                "Repository discovery failed."),
            new(
                "repository.pull_requests",
                false,
                "Repository discovery failed."),
            new(
                "repository.actions",
                false,
                "Repository discovery failed."),
            new(
                "repository.workspace_sync",
                false,
                "Repository discovery failed.")
        ];

    private static ConnectorResourceDto MapRepository(
        System.Text.Json.JsonElement item)
    {
        var id = item.TryGetProperty("id", out var idNode)
            ? idNode.GetInt64().ToString(
                System.Globalization.CultureInfo.InvariantCulture)
            : GitHubApiJson.String(item, "node_id")
              ?? Guid.NewGuid().ToString("N");

        var name = GitHubApiJson.String(item, "name") ?? "repository";
        var fullName =
            GitHubApiJson.String(item, "full_name") ?? name;

        return new ConnectorResourceDto(
            id,
            "repository",
            name,
            fullName,
            GitHubApiJson.String(item, "html_url"),
            GitHubApiJson.String(item, "default_branch"),
            GitHubApiJson.Bool(item, "private"),
            GitHubApiJson.Bool(item, "archived"),
            item.TryGetProperty("language", out var language)
            && language.ValueKind
            == System.Text.Json.JsonValueKind.String
                ? language.GetString()
                : null,
            GitHubApiJson.String(item, "description"),
            GitHubApiJson.Date(item, "updated_at"));
    }

    private static bool Matches(
        ConnectorResourceDto item,
        ConnectorDiscoveryQuery query)
    {
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var search = query.Search.Trim();

            if (!item.FullName.Contains(
                    search,
                    StringComparison.OrdinalIgnoreCase)
                && !(item.Description?.Contains(
                         search,
                         StringComparison.OrdinalIgnoreCase)
                     ?? false)
                && !(item.PrimaryLanguage?.Contains(
                         search,
                         StringComparison.OrdinalIgnoreCase)
                     ?? false))
            {
                return false;
            }
        }

        var visibility = query.Visibility?
            .Trim()
            .ToLowerInvariant();

        return visibility switch
        {
            "private" => item.Private,
            "public" => !item.Private,
            _ => true
        };
    }

    private static async Task<bool> IsSuccessAsync(
        HttpClient client,
        string path,
        CancellationToken cancellationToken)
    {
        try
        {
            using var response = await client.GetAsync(
                path,
                cancellationToken);
            return response.IsSuccessStatusCode;
        }
        catch (HttpRequestException)
        {
            return false;
        }
    }
}
