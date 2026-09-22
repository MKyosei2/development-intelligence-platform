// Purpose: Defines a focused responsibility within the development intelligence platform.
namespace Dip.Application.Abstractions.GitHub;
public interface IGitHubWebhookVerifier { bool Verify(ReadOnlySpan<byte> payload, string? signatureHeader); }
