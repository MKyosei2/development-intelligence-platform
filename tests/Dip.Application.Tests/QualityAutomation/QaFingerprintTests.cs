// Purpose: Defines QA Fingerprint Tests responsibilities for this repository area.
using Dip.Application.Features.QualityAutomation;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;
namespace Dip.Application.Tests.QualityAutomation;
public sealed class QaFingerprintTests
{
    [Fact]
    public void SameFinding_RelativeAndAbsoluteLikePath_HasStableFingerprint()
    {
        var a = new QaFindingCandidate("runtime", Severity.Critical, "python-runtime", "Unhandled TypeError", "bad type", "samples/bug/app.py", 12, ExceptionType: "TypeError", StackTrace: "Traceback (most recent call last):\n...");
        var b = a with { FilePath = "C:/repo/samples/bug/app.py" };
        QaFingerprint.Create(LanguageId.Python, a).Should().Be(QaFingerprint.Create(LanguageId.Python, b));
    }
    [Fact]
    public void DifferentException_ProducesDifferentFingerprint()
    {
        var a = new QaFindingCandidate("runtime", Severity.Critical, "runtime", "A", "x", "app.py", 1, ExceptionType: "TypeError");
        var b = a with { ExceptionType = "ValueError" }; QaFingerprint.Create(LanguageId.Python, a).Should().NotBe(QaFingerprint.Create(LanguageId.Python, b));
    }
}
