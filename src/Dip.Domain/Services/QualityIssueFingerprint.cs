using System.Security.Cryptography;
using System.Text;
namespace Dip.Domain.Services;

public static class QualityIssueFingerprint
{
    public static string Create(Guid projectId,string category,string? ruleId,string? source,string title)
    {
        static string N(string? v)=>string.IsNullOrWhiteSpace(v)?string.Empty:
            string.Join(' ',v.Trim().ToLowerInvariant().Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
        var raw=string.Join("|",projectId.ToString("N"),N(category),N(ruleId),N(source),N(title));
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
    }
}
