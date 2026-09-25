// Purpose: Implements Project Intelligence Source Options infrastructure responsibilities for project intelligence.
namespace Dip.Infrastructure.ProjectIntelligence;

public sealed class ProjectIntelligenceSourceOptions
{
    public const string SectionName = "ProjectIntelligenceSources";
    public RedmineSourceOptions Redmine { get; set; } = new();
    public SlackSourceOptions Slack { get; set; } = new();
    public TeamsSourceOptions Teams { get; set; } = new();

    public sealed class RedmineSourceOptions
    {
        public string BaseUrl { get;
         set;
         } = "";
         public string ApiKey { get;
         set;
         } = "";
         public string ProjectIdentifier { get;
         set;
         } = "";
         public bool IncludeClosed { get;
         set;
         } = true;
         public int MaxPages { get;
         set;
         } = 100;
        public Dictionary<string, string> ProjectIdentifiers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Identifier(Guid projectId) => ProjectIdentifiers.TryGetValue(projectId.ToString(), out var x) && !string.IsNullOrWhiteSpace(x) ? x : ProjectIdentifier;
    }
    public sealed class SlackSourceOptions
    {
        public string BotToken { get;
         set;
         } = "";
         public string SigningSecret { get;
         set;
         } = "";
         public string[] ChannelIds { get;
         set;
         } = [];
         public int HistoryDays { get;
         set;
         } = 30;
         public int MaxPagesPerChannel { get;
         set;
         } = 20;
        public Dictionary<string, string[]> ProjectChannelIds { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public IReadOnlyList<string> Channels(Guid projectId) => ProjectChannelIds.TryGetValue(projectId.ToString(), out var x) && x.Length > 0 ? x : ChannelIds;
    }
    public sealed class TeamsSourceOptions
    {
        public string IngestKey { get; set; } = "";
        public Dictionary<string, string> ProjectIngestKeys { get; set; } = new(StringComparer.OrdinalIgnoreCase);
        public string Key(Guid projectId) => ProjectIngestKeys.TryGetValue(projectId.ToString(), out var x) && !string.IsNullOrWhiteSpace(x) ? x : IngestKey;
    }
}
