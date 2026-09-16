// Purpose: Defines the I Gi tH ub We bh oo kV er if ie r application boundary.
namespace Dip.Application.Abstractions.GitHub;
public interface IGitHubWebhookVerifier{bool Verify(ReadOnlySpan<byte> payload,string? signatureHeader);}
