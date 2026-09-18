// Purpose: Defines the application boundary represented by I Gi tH ub We bh oo kV er if ie r.
namespace Dip.Application.Abstractions.GitHub;
public interface IGitHubWebhookVerifier{bool Verify(ReadOnlySpan<byte> payload,string? signatureHeader);}
