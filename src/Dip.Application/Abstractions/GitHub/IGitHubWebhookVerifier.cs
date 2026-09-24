// Purpose: Implements I Git Hub Webhook Verifier responsibilities for git hub.
namespace Dip.Application.Abstractions.GitHub;
public interface IGitHubWebhookVerifier { bool Verify(ReadOnlySpan<byte> payload, string? signatureHeader); }
