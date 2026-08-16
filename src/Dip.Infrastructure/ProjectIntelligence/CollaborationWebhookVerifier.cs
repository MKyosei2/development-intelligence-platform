using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Dip.Infrastructure.ProjectIntelligence;

public sealed class CollaborationWebhookVerifier
{
    private readonly ProjectIntelligenceSourceOptions _o;public CollaborationWebhookVerifier(IOptions<ProjectIntelligenceSourceOptions> options)=>_o=options.Value;
    public bool VerifySlack(string timestamp,string signature,string rawBody,DateTimeOffset now)
    {
        if(string.IsNullOrWhiteSpace(_o.Slack.SigningSecret)||string.IsNullOrWhiteSpace(timestamp)||string.IsNullOrWhiteSpace(signature)||!long.TryParse(timestamp,out var sec))return false;if(Math.Abs((now-DateTimeOffset.FromUnixTimeSeconds(sec)).TotalMinutes)>5)return false;var baseString=$"v0:{timestamp}:{rawBody}";using var hmac=new HMACSHA256(Encoding.UTF8.GetBytes(_o.Slack.SigningSecret));var expected="v0="+Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(baseString))).ToLowerInvariant();return Fixed(expected,signature);
    }
    public bool IsSlackChannelAllowed(Guid projectId,string? channel)
    {
        if(string.IsNullOrWhiteSpace(channel))return false;
        var allowed=_o.Slack.Channels(projectId);
        return allowed.Any(x=>x.Equals(channel,StringComparison.Ordinal));
    }
    public bool VerifyTeams(Guid projectId,string key){var expected=_o.Teams.Key(projectId);return !string.IsNullOrWhiteSpace(expected)&&Fixed(expected,key);}
    private static bool Fixed(string a,string b){var x=SHA256.HashData(Encoding.UTF8.GetBytes(a));var y=SHA256.HashData(Encoding.UTF8.GetBytes(b));return CryptographicOperations.FixedTimeEquals(x,y);}
}
