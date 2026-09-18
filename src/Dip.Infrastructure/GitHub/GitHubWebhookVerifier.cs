// Purpose: Verifies external input or runtime state through G it Hu bW eb ho ok Ve ri fi er.
using System.Security.Cryptography;using System.Text;using Dip.Application.Abstractions.GitHub;using Microsoft.Extensions.Options;
namespace Dip.Infrastructure.GitHub;
public sealed class GitHubWebhookVerifier:IGitHubWebhookVerifier
{
 readonly byte[] _secret;public GitHubWebhookVerifier(IOptions<GitHubOptions> options){var secret=options.Value.WebhookSecret;if(string.IsNullOrWhiteSpace(secret))throw new InvalidOperationException("GitHub:WebhookSecret is missing.");_secret=Encoding.UTF8.GetBytes(secret);}
 public bool Verify(ReadOnlySpan<byte> payload,string? signatureHeader){if(string.IsNullOrWhiteSpace(signatureHeader))return false;const string prefix="sha256=";if(!signatureHeader.StartsWith(prefix,StringComparison.OrdinalIgnoreCase))return false;var hex=signatureHeader[prefix.Length..];if(hex.Length!=64)return false;byte[] supplied;try{supplied=Convert.FromHexString(hex);}catch(FormatException){return false;}using var hmac=new HMACSHA256(_secret);var expected=hmac.ComputeHash(payload.ToArray());return CryptographicOperations.FixedTimeEquals(expected,supplied);}
}
