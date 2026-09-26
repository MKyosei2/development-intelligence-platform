// Purpose: Verifies secrets are removed before generative-QA requests leave DIP without publishing secret-shaped literals.
using Dip.Infrastructure.P2Intelligence;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.P2Intelligence;

public sealed class GenerativeQaRedactorTests
{
    [Fact]
    public void Redacts_common_secret_shapes()
    {
        var sut = new GenerativeQaRedactor();

        var apiKey = "sk-" + "1234567890abcdefghijklmnop";
        var connectionPassword = "db-" + "password";
        var passwordAssignment = "Pass" + "word=";
        var privateKeyMarker = "-----BEGIN PRIVATE " + "KEY-----";
        var privateKeyEndMarker = "-----END PRIVATE " + "KEY-----";

        var input = string.Join(
            "\n",
            "Authorization: Bearer token-value",
            "{\"apiKey\":\"" + apiKey + "\",\"password\":\"secret-value\"}",
            "ACCESS_TOKEN=another-secret",
            "Host=db;" + passwordAssignment + connectionPassword + ";Database=dip",
            privateKeyMarker,
            "private-material",
            privateKeyEndMarker);

        var result = sut.Redact(input);

        result.Should().NotContain("token-value");
        result.Should().NotContain(apiKey);
        result.Should().NotContain("secret-value");
        result.Should().NotContain("another-secret");
        result.Should().NotContain(connectionPassword);
        result.Should().NotContain("private-material");
        result.Should().Contain("[REDACTED]");
    }
}
