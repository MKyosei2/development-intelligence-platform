// Purpose: Implements Work Item Integration Options infrastructure responsibilities for work items.
namespace Dip.Infrastructure.WorkItems;
public sealed class WorkItemIntegrationOptions
{
    public const string SectionName = "WorkItems";
    public JiraOptions Jira { get; set; } = new();
    public LinearOptions Linear { get; set; } = new();
    public sealed class JiraOptions { public string BaseUrl { get;
     set;
     } = string.Empty;
     public string Email { get;
     set;
     } = string.Empty;
     public string ApiToken { get;
     set;
     } = string.Empty;
     public string ProjectKey { get;
     set;
     } = string.Empty;
     public string IssueType { get;
     set;
     } = "Bug";
     }
    public sealed class LinearOptions { public string Endpoint { get;
     set;
     } = "https://api.linear.app/graphql";
     public string ApiKey { get;
     set;
     } = string.Empty;
     public string TeamId { get;
     set;
     } = string.Empty;
     }
}
