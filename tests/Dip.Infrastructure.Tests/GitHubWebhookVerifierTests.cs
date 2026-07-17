using System.Security.Cryptography;
using System.Text;
using Dip.Infrastructure.GitHub;
using Microsoft.Extensions.Options;
using Xunit;

namespace Dip.Infrastructure.Tests;

public sealed class GitHubWebhookVerifierTests
{
    [Fact]
    public void Verify_AcceptsValidSignature()
    {
        const string secret = "test-secret";
        var payload = Encoding.UTF8.GetBytes("{\"action\":\"opened\"}");

        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(secret));
        var signature = "sha256=" + Convert.ToHexString(
            hmac.ComputeHash(payload)).ToLowerInvariant();

        var verifier = new GitHubWebhookVerifier(
            Options.Create(new GitHubOptions
            {
                WebhookSecret = secret
            }));

        Assert.True(verifier.Verify(payload, signature));
    }

    [Fact]
    public void Verify_RejectsInvalidSignature()
    {
        var verifier = new GitHubWebhookVerifier(
            Options.Create(new GitHubOptions
            {
                WebhookSecret = "test-secret"
            }));

        Assert.False(verifier.Verify(
            Encoding.UTF8.GetBytes("{}"),
            "sha256=" + new string('0', 64)));
    }
}
