// Purpose: Defines validated configuration for selecting the local credential store or external secrets broker.
namespace Dip.Infrastructure.Security;

public sealed class SecretsBrokerOptions
{
    public const string SectionName = "DIP:Secrets";

    public string Provider { get; set; } = "local";
    public string? BrokerUrl { get; set; }
    public string? BrokerToken { get; set; }

    public bool UseBroker => string.Equals(Provider, "broker", StringComparison.OrdinalIgnoreCase);

    public bool IsValid(out string? error)
    {
        if (!string.Equals(Provider, "local", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(Provider, "broker", StringComparison.OrdinalIgnoreCase))
        {
            error = "DIP:Secrets:Provider must be either 'local' or 'broker'.";
            return false;
        }

        if (UseBroker
            && (!Uri.TryCreate(BrokerUrl, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)))
        {
            error = "DIP:Secrets:BrokerUrl must be an absolute HTTP(S) URL when broker mode is enabled.";
            return false;
        }

        error = null;
        return true;
    }
}
