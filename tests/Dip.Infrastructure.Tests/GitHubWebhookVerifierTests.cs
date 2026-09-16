// Purpose: Verifies G it Hu bW eb ho ok Ve ri fi er Te st s behavior.
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
    [Fact]
    public void PullRequestBackfillPaginationDefaults_AreBoundedAndCoverLargeRepositories()
    {
        var options = new GitHubOptions();

        Assert.InRange(options.MaxPullRequestPages, 2, 50);
        Assert.Equal(3, options.MaxPullRequestCommitPages);
        Assert.InRange(options.MaxPullRequestFilePages, 2, 30);
        Assert.InRange(options.MaxPullRequestReviewPages, 2, 50);
    }

}
