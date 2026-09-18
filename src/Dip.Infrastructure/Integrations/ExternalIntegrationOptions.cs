// Purpose: Defines configuration options for E xt er na lI nt eg ra ti on Op ti on s.
namespace Dip.Infrastructure.Integrations;

public sealed class PerforceOptions
{
    public const string SectionName="Perforce";public string Executable{get;init;}="p4";public string? WorkingDirectory{get;init;}=null;public int TimeoutSeconds{get;init;}=30;
}
public sealed class JenkinsOptions
{
    public const string SectionName="Jenkins";public string BaseUrl{get;init;}=string.Empty;public string User{get;init;}=string.Empty;public string ApiToken{get;init;}=string.Empty;public int TimeoutSeconds{get;init;}=30;
}
