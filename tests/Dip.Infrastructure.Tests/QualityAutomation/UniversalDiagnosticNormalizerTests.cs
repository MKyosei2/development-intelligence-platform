// Purpose: Verifies Universal Diagnostic Normalizer behavior, edge cases, and repository invariants.
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class UniversalDiagnosticNormalizerTests
{
    [Fact]
    public void Sarif_NormalizesRuleSeverityAndLocation()
    {
        var path = Temp(".sarif", """{"version":"2.1.0","runs":[{"results":[{"ruleId":"X001","level":"error","message":{"text":"bad thing"},"locations":[{"physicalLocation":{"artifactLocation":{"uri":"src/a.ts"},"region":{"startLine":7,"startColumn":3}}}]}]}]}""");
        try
        {
            var finding = new UniversalDiagnosticNormalizer().NormalizeReportFile(path, LanguageId.TypeScript, "sarif").Single();
            finding.RuleId.Should().Be("X001");
            finding.Severity.Should().Be(Severity.Critical);
            finding.FilePath.Should().Be("src/a.ts");
            finding.Line.Should().Be(7);
            finding.DiagnosticSchemaVersion.Should().Be(UniversalDiagnosticNormalizer.CurrentSchemaVersion);
            finding.SourceFormat.Should().Be("sarif");
            finding.SourceFormatVersion.Should().Be("2.1.0");
        }
        finally
        {
            File.Delete(path);
        }
    }
    [Fact]
    public void Junit_NormalizesFailures()
    {
        var path = Temp(".xml", "<testsuite><testcase name=\"works\"/><testcase name=\"fails\"><failure message=\"expected 1\">trace</failure></testcase></testsuite>");
        try
        {
            var finding = new UniversalDiagnosticNormalizer().NormalizeReportFile(path, LanguageId.Java, "junit").Single();
            finding.Category.Should().Be("test");
            finding.Message.Should().Contain("expected 1");
            finding.DiagnosticSchemaVersion.Should().Be("dip.finding.v2");
            finding.SourceFormat.Should().Be("junit");
        }
        finally
        {
            File.Delete(path);
        }
    }
    [Fact]
    public void GenericJson_NormalizesDiagnostics()
    {
        var path = Temp(".json", "[{\"filePath\":\"src/app.js\",\"line\":4,\"column\":2,\"ruleId\":\"no-undef\",\"severity\":\"error\",\"message\":\"x is not defined\"}]");
        try
        {
            var finding = new UniversalDiagnosticNormalizer().NormalizeReportFile(path, LanguageId.JavaScript, "eslint").Single();
            finding.RuleId.Should().Be("no-undef");
            finding.Severity.Should().Be(Severity.Critical);
        }
        finally
        {
            File.Delete(path);
        }
    }
    [Theory]
    [InlineData("System.DivideByZeroException", "DivideByZeroException")]
    [InlineData("System.NullReferenceException", "NullReferenceException")]
    public void CSharpText_NormalizesQualifiedExceptionType(string qualifiedType, string expectedType)
    {
        var normalizer = new UniversalDiagnosticNormalizer();

        var findings = normalizer.NormalizeText(
            LanguageId.CSharp,
            "dotnet-runtime",
            string.Empty,
            $"Unhandled exception. {qualifiedType}: deterministic bug-zoo failure");

        var runtime = findings.Single(finding => finding.Category == "runtime");
        runtime.ExceptionType.Should().Be(expectedType);
        runtime.Severity.Should().Be(Severity.Critical);
    }

    private static string Temp(string extension, string content)
    {
        var path = Path.Combine(Path.GetTempPath(), "dip-normalizer-" + Guid.NewGuid().ToString("N") + extension);
        File.WriteAllText(path, content);
        return path;
    }
}
