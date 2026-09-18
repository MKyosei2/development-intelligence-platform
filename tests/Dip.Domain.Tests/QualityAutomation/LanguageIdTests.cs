// Purpose: Verifies behavior and invariants covered by L an gu ag eI dT es ts.
using System.Text.Json;
using Dip.Domain.ValueObjects;
using FluentAssertions;

namespace Dip.Domain.Tests.QualityAutomation;

public sealed class LanguageIdTests
{
    [Theory]
    [InlineData("Python","python")]
    [InlineData("C#","csharp")]
    [InlineData("C++","cpp")]
    [InlineData("TypeScript","typescript")]
    [InlineData("my-proprietary-lang","my-proprietary-lang")]
    public void Parse_NormalizesBuiltinsButAllowsExtensions(string input,string expected)=>LanguageId.Parse(input).Value.Should().Be(expected);

    [Fact]
    public void JsonConverter_ReadsLegacyEnumNumbersAndWritesStrings()
    {
        JsonSerializer.Deserialize<LanguageId>("1").Should().Be(LanguageId.CSharp);
        JsonSerializer.Serialize(LanguageId.Parse("fortran")).Should().Be("\"fortran\"");
    }
}
