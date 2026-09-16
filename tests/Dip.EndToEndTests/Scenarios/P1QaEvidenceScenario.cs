// Purpose: Verifies P1 Qa Ev id en ce Sc en ar io behavior.
using System.Diagnostics;
using Dip.EndToEndTests.Support;
using FluentAssertions;

namespace Dip.EndToEndTests.Scenarios;

public sealed class P1QaEvidenceScenario
{
    [Fact]
    public async Task PythonRuntimeFailure_IsDetectedReproducedAndReturnedAsEvidence()
    {
        if (!PythonAvailable())
            return;

        // This QA request may execute up to five bounded reproduction attempts.
        // Keep the server-side per-attempt timeout at 15 seconds, but give this
        // specific E2E HTTP call enough bounded headroom to complete normally.
        using var api = new DipScenarioClient(TimeSpan.FromSeconds(120));
        var project = await api.CreateProjectAsync("p1-python-qa");
        var qa = await api.RunPythonQaAsync(project);

        qa.CriticalFindingCount.Should().BeGreaterThan(0);
        qa.Findings.Should().Contain(
            x => x.Category == "runtime" && x.Severity == "Critical");
        qa.Findings.Should().Contain(
            x => x.Category == "runtime"
                 && x.ReproductionStatus == "Reproduced"
                 && x.ReproducedAttempts > 0);
    }

    private static bool PythonAvailable()
    {
        try
        {
            using var process = Process.Start(
                new ProcessStartInfo
                {
                    FileName = OperatingSystem.IsWindows() ? "python" : "python3",
                    ArgumentList = { "--version" },
                    UseShellExecute = false,
                    CreateNoWindow = true
                });

            process?.WaitForExit(3000);
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }
}
