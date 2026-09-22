// Purpose: Implements domain behavior for Q ua li ty Is su eF in ge rp ri nt.
using System.Security.Cryptography;
    using System.Text;
    namespace Dip.Domain.Services;
    public static class QualityIssueFingerprint { public static string Create(Guid projectId,string? category,string? ruleId,string? source,string? title){var text=string.Join("|",projectId.ToString("N"),Normalize(category),Normalize(ruleId),Normalize(source),Normalize(title));
    return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
    }
    private static string Normalize(string? value)=>string.IsNullOrWhiteSpace(value)?string.Empty:value.Trim().ToLowerInvariant();
    }
