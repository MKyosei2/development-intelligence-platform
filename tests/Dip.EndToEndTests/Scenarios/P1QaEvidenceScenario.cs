using System.Diagnostics;
using Dip.EndToEndTests.Support;
using FluentAssertions;
namespace Dip.EndToEndTests.Scenarios;
public sealed class P1QaEvidenceScenario
{
 [Fact]
 public async Task PythonRuntimeFailure_IsDetectedReproducedAndReturnedAsEvidence()
 {
  if(!PythonAvailable())return;
  using var api=new DipScenarioClient();var project=await api.CreateProjectAsync("p1-python-qa");var qa=await api.RunPythonQaAsync(project);
  qa.CriticalFindingCount.Should().BeGreaterThan(0);
  qa.Findings.Should().Contain(x=>x.Category=="runtime"&&x.Severity=="Critical");
  qa.Findings.Should().Contain(x=>x.Category=="runtime"&&x.ReproductionStatus=="Reproduced"&&x.ReproducedAttempts>0);
 }
 private static bool PythonAvailable(){try{using var p=Process.Start(new ProcessStartInfo{FileName=OperatingSystem.IsWindows()?"python":"python3",ArgumentList={"--version"},UseShellExecute=false,CreateNoWindow=true});p?.WaitForExit(3000);return p?.ExitCode==0;}catch{return false;}}
}
