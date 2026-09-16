// Purpose: Defines the I In te gr at io nC re de nt ia lS to re application boundary.
namespace Dip.Application.Abstractions.Security;

public interface IIntegrationCredentialStore
{
    Task<string> StoreAsync(
        string provider,
        string secretJson,
        CancellationToken ct);

    Task<string?> GetAsync(
        string credentialReference,
        CancellationToken ct);

    Task DeleteAsync(
        string credentialReference,
        CancellationToken ct);
}
