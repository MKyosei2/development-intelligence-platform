// Purpose: Verifies cross-file lock-order cycle detection without claiming a runtime deadlock occurrence.
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;
using FluentAssertions;

namespace Dip.Infrastructure.Tests.QualityAutomation;

public sealed class UniversalSemanticAnalyzerConcurrencyTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dip-semantic-lock-" + Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task AnalyzeAsync_ReportsConflictingLockOrderAcrossFiles()
    {
        Directory.CreateDirectory(_root);
        await File.WriteAllTextAsync(Path.Combine(_root, "A.cs"), "class A { object gateA=new(); object gateB=new(); void F(){ lock(gateA){ lock(gateB){} } } }");
        await File.WriteAllTextAsync(Path.Combine(_root, "B.cs"), "class B { object gateA=new(); object gateB=new(); void G(){ lock(gateB){ lock(gateA){} } } }");

        var result = await new UniversalSemanticAnalyzer().AnalyzeAsync(
            new QaSemanticAnalysisRequest(LanguageId.CSharp, _root),
            CancellationToken.None);

        result.Findings.Should().Contain(x => x.RuleId == "DIP-CON004" && x.Severity.ToString() == "Critical");
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
            Directory.Delete(_root, true);
    }
}
