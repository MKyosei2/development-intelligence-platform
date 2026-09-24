// Purpose: Synchronizes a bound GitHub repository head into immutable DIP evidence observations.
using System.Text.Json;
using Dip.Application.Features.Integrations;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Infrastructure.Integrations.GitHub;

internal sealed class GitHubSyncService
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    internal async Task<ConnectorSyncResult> SyncWithClientAsync(
        IntegrationConnection connection,
        ProjectSourceBinding binding,
        HttpClient client,
        CancellationToken cancellationToken)
    {
        if (!binding.ResourceType.Equals(
                "repository",
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "GitHub connector currently syncs repository resources only.");
        }

        var parts = binding.ExternalResourceName.Split('/', 2);

        if (parts.Length != 2
            || string.IsNullOrWhiteSpace(parts[0])
            || string.IsNullOrWhiteSpace(parts[1]))
        {
            throw new InvalidOperationException(
                "GitHub binding ExternalResourceName must be owner/repository.");
        }

        var branch = BindingBranch(binding.ConfigurationJson);

        if (string.IsNullOrWhiteSpace(branch))
        {
            using var repositoryResponse = await client.GetAsync(
                $"repos/{GitHubApiJson.Escape(parts[0])}/{GitHubApiJson.Escape(parts[1])}",
                cancellationToken);

            repositoryResponse.EnsureSuccessStatusCode();

            using var repositoryDocument =
                await GitHubApiJson.ReadJsonAsync(
                    repositoryResponse,
                    cancellationToken);

            branch = GitHubApiJson.String(
                         repositoryDocument.RootElement,
                         "default_branch")
                     ?? "main";
        }

        using var response = await client.GetAsync(
            $"repos/{GitHubApiJson.Escape(parts[0])}/{GitHubApiJson.Escape(parts[1])}"
            + $"/commits/{GitHubApiJson.Escape(branch)}",
            cancellationToken);

        response.EnsureSuccessStatusCode();

        using var document = await GitHubApiJson.ReadJsonAsync(
            response,
            cancellationToken);

        var root = document.RootElement;

        var sha = GitHubApiJson.String(root, "sha")
                  ?? throw new InvalidOperationException(
                      "GitHub commit response did not contain SHA.");

        var commit = root.TryGetProperty("commit", out var commitNode)
            ? commitNode
            : default;

        var message =
            commit.ValueKind == JsonValueKind.Object
                ? GitHubApiJson.String(commit, "message")
                    ?.Split('\n')[0]
                : null;

        var observed = DateTimeOffset.UtcNow;

        if (commit.ValueKind == JsonValueKind.Object
            && commit.TryGetProperty(
                "committer",
                out var committer))
        {
            observed = GitHubApiJson.Date(
                           committer,
                           "date")
                       ?? observed;
        }

        var actor =
            root.TryGetProperty("author", out var author)
            && author.ValueKind == JsonValueKind.Object
                ? GitHubApiJson.String(author, "login")
                : null;

        var url = GitHubApiJson.String(root, "html_url");

        var metadata = JsonSerializer.Serialize(
            new
            {
                repository = binding.ExternalResourceName,
                repositoryId = binding.ExternalResourceId,
                branch,
                commitSha = sha,
                message,
                url
            },
            JsonOptions);

        var observation = new ConnectorSyncObservation(
            EvidenceSourceType.GitHub,
            $"connection:{connection.Id:N}:repository:{binding.ExternalResourceId}",
            $"github:commit:{sha}",
            EvidenceEventType.SourceCommit,
            $"repo:{binding.ExternalResourceName}",
            "github.commit",
            $"GitHub commit {GitHubApiJson.Short(sha)}",
            string.IsNullOrWhiteSpace(message)
                ? $"Observed {binding.ExternalResourceName}@{GitHubApiJson.Short(sha)}."
                : message,
            EvidenceOutcome.Changed,
            EvidenceConfidence.High,
            observed,
            url,
            actor,
            metadata);

        return new ConnectorSyncResult(
            Observed: 1,
            Observations: [observation],
            Warnings: Array.Empty<string>());
    }

    private static string? BindingBranch(string? configurationJson)
    {
        if (string.IsNullOrWhiteSpace(configurationJson))
            return null;

        try
        {
            using var document = JsonDocument.Parse(configurationJson);
            return document.RootElement.TryGetProperty(
                       "defaultBranch",
                       out var branch)
                   && branch.ValueKind == JsonValueKind.String
                ? branch.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
