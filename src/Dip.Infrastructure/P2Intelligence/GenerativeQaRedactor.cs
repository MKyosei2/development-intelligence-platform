// Purpose: Removes common credentials and secrets before repository context or evidence leaves DIP.
using System.Text.RegularExpressions;

namespace Dip.Infrastructure.P2Intelligence;

public sealed partial class GenerativeQaRedactor
{
    private const string Replacement = "[REDACTED]";

    public string Redact(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return value ?? string.Empty;

        var redacted = AuthorizationHeader().Replace(value, "$1" + Replacement);
        redacted = JsonSecret().Replace(redacted, "$1\"" + Replacement + "\"");
        redacted = AssignmentSecret().Replace(redacted, "$1" + Replacement);
        redacted = OpenAiStyleKey().Replace(redacted, Replacement);
        redacted = PrivateKeyBlock().Replace(redacted, Replacement);
        redacted = ConnectionPassword().Replace(redacted, "$1" + Replacement);
        return redacted;
    }

    [GeneratedRegex(@"(?im)(authorization\s*[:=]\s*(?:bearer\s+|basic\s+)?)[^\s,;]+", RegexOptions.CultureInvariant)]
    private static partial Regex AuthorizationHeader();

    [GeneratedRegex(@"(?i)(""(?:api[_-]?key|access[_-]?token|refresh[_-]?token|password|secret|client[_-]?secret)""\s*:\s*)""[^""]*""", RegexOptions.CultureInvariant)]
    private static partial Regex JsonSecret();

    [GeneratedRegex(@"(?im)((?:api[_-]?key|access[_-]?token|refresh[_-]?token|password|secret|client[_-]?secret)\s*=\s*)[^\s;]+", RegexOptions.CultureInvariant)]
    private static partial Regex AssignmentSecret();

    [GeneratedRegex(@"\bsk-[A-Za-z0-9_-]{16,}\b", RegexOptions.CultureInvariant)]
    private static partial Regex OpenAiStyleKey();

    [GeneratedRegex(@"-----BEGIN [A-Z ]*PRIVATE KEY-----[\s\S]*?-----END [A-Z ]*PRIVATE KEY-----", RegexOptions.CultureInvariant)]
    private static partial Regex PrivateKeyBlock();

    [GeneratedRegex(@"(?i)(password\s*=\s*)[^;\r\n]+", RegexOptions.CultureInvariant)]
    private static partial Regex ConnectionPassword();
}
