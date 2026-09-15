using Dip.Agent.Core;
using Microsoft.Extensions.Configuration;

namespace Dip.Agent.Windows;

internal static class AgentConfiguration
{
    public static AgentOptions Load(IConfiguration configuration)
    {
        static string Required(IConfiguration c, string key)
            => string.IsNullOrWhiteSpace(c[key])
                ? throw new InvalidOperationException($"Missing required configuration: {key}")
                : c[key]!.Trim();

        var api = new Uri(Required(configuration, "DIP_AGENT_API_URL"), UriKind.Absolute);
        var projectId = Guid.Parse(Required(configuration, "DIP_AGENT_PROJECT_ID"));
        var projectRoot = Path.GetFullPath(Required(configuration, "DIP_AGENT_PROJECT_ROOT"));
        var agentId = configuration["DIP_AGENT_ID"]?.Trim();
        if (string.IsNullOrWhiteSpace(agentId))
            agentId = Environment.MachineName;
        var rootId = configuration["DIP_AGENT_ROOT_ID"]?.Trim();
        if (string.IsNullOrWhiteSpace(rootId))
            rootId = "project-root";

        var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(local))
            local = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".dip");
        var state = configuration["DIP_AGENT_STATE_DB"]?.Trim();
        if (string.IsNullOrWhiteSpace(state))
            state = Path.Combine(local, "DIP", "agent", projectId.ToString("N"), rootId, "agent-state.db");

        var pollSeconds = int.TryParse(configuration["DIP_AGENT_POLL_SECONDS"], out var parsed)
            ? Math.Clamp(parsed, 2, 3600)
            : 30;
        var batchSize = int.TryParse(configuration["DIP_AGENT_BATCH_SIZE"], out var batch)
            ? Math.Clamp(batch, 1, 500)
            : 100;
        var maxMediaMb = long.TryParse(configuration["DIP_AGENT_MAX_MEDIA_MB"], out var parsedMedia)
            ? Math.Clamp(parsedMedia, 1, 2048)
            : 1024;

        var excluded = AgentOptions.DefaultExcludedDirectories
            .Concat((configuration["DIP_AGENT_EXCLUDED_DIRS"] ?? string.Empty)
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return AgentOptions.Validate(new AgentOptions(
            api,
            projectId,
            agentId,
            rootId,
            projectRoot,
            state,
            configuration["DIP_AGENT_BEARER_TOKEN"],
            TimeSpan.FromSeconds(pollSeconds),
            excluded,
            batchSize,
            configuration["DIP_AGENT_PIPE_NAME"],
            configuration["DIP_AGENT_BRIDGE_TOKEN"],
            configuration["DIP_AGENT_MEDIA_SPOOL"],
            maxMediaMb * 1024L * 1024L));
    }
}
