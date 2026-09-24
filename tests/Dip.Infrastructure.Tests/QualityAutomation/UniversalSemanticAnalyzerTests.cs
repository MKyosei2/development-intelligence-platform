// Purpose: Verifies U ni ve rs al Se ma nt ic An al yz er behavior, edge cases, and invariants for q ua li ty au to ma ti on.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.ValueObjects;
using Dip.Infrastructure.QualityAutomation;
namespace Dip.Infrastructure.Tests.QualityAutomation;
public sealed class UniversalSemanticAnalyzerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "dip-semantic-tests", Guid.NewGuid().ToString("N"));
    public UniversalSemanticAnalyzerTests() => Directory.CreateDirectory(_root);
    [Fact] public async Task Detects_bounded_untrusted_flow_to_process_sink() { await File.WriteAllTextAsync(Path.Combine(_root, "Program.cs"),
    "var arg = Request.Query[\"x\"];\nProcess.Start(arg);\n");
     var result = await new UniversalSemanticAnalyzer().AnalyzeAsync(new(LanguageId.CSharp, _root), default);
     Assert.Contains(result.Findings, x => x.RuleId == "DIP-DF001");
     }
    [Fact] public async Task Clean_local_value_does_not_trigger_taint_rule() { await File.WriteAllTextAsync(Path.Combine(_root, "app.py"), "value = 'safe'\nprint(value)\n");
     var result = await new UniversalSemanticAnalyzer().AnalyzeAsync(new(LanguageId.Python, _root), default);
     Assert.DoesNotContain(result.Findings, x => x.RuleId == "DIP-DF001");
     }
    public void Dispose() { try { Directory.Delete(_root, true); } catch {
        // Intentionally ignored to preserve the existing best-effort or fallback behavior.
    } }
}
