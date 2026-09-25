// Purpose: Verifies secrets are removed before generative-QA requests leave DIP.
using Dip.Infrastructure.P2Intelligence;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.P2Intelligence;

public sealed class GenerativeQaRedactorTests
{
    [Fact]
    public void Redacts_common_secret_shapes()
    {
        var sut = new GenerativeQaRedactor();
        var input = """
Authorization: Bearer token-value
{"apiKey":"sk-1234567890abcdefghijklmnop","password":"secret-value"}
ACCESS_TOKEN=another-secret
Host=db;Password=db-password;Database=dip
-----BEGIN PRIVATE KEY-----
private-material
-----END PRIVATE KEY-----
""";

        var result = sut.Redact(input);

        result.Should().NotContain("token-value");
        result.Should().NotContain("sk-1234567890abcdefghijklmnop");
        result.Should().NotContain("secret-value");
        result.Should().NotContain("another-secret");
        result.Should().NotContain("db-password");
        result.Should().NotContain("private-material");
        result.Should().Contain("[REDACTED]");
    }
}
