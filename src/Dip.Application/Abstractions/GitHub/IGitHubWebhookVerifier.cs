namespace Dip.Application.Abstractions.GitHub;
public interface IGitHubWebhookVerifier{bool Verify(ReadOnlySpan<byte> payload,string? signatureHeader);}
