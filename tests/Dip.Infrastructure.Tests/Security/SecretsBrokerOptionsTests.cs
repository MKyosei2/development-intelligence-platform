// Purpose: Verifies secrets-broker configuration fails closed before runtime HTTP operations begin.
using Dip.Infrastructure.Security;

namespace Dip.Infrastructure.Tests.Security;

public sealed class SecretsBrokerOptionsTests
{
    [Fact]
    public void LocalProvider_DoesNotRequireBrokerUrl()
    {
        var options = new SecretsBrokerOptions { Provider = "local" };

        Assert.True(options.IsValid(out var error));
        Assert.Null(error);
    }

    [Fact]
    public void BrokerProvider_RequiresAbsoluteHttpUrl()
    {
        var options = new SecretsBrokerOptions
        {
            Provider = "broker",
            BrokerUrl = "not-a-url"
        };

        Assert.False(options.IsValid(out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void BrokerProvider_AcceptsHttpsUrl()
    {
        var options = new SecretsBrokerOptions
        {
            Provider = "broker",
            BrokerUrl = "https://secrets.example.test/"
        };

        Assert.True(options.IsValid(out var error));
        Assert.Null(error);
    }
}
