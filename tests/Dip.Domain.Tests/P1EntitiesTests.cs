// Purpose: Verifies behavior and invariants covered by P1 En ti ti es Te st s.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Dip.Domain.ValueObjects;
using FluentAssertions;
namespace Dip.Domain.Tests;
public sealed class P1EntitiesTests
{
 [Fact]public void QaRun_TracksFinalFindingCounts()
 {
  var now=DateTimeOffset.UtcNow;var run=new QaRun(Guid.NewGuid(),Guid.NewGuid(),"project",LanguageId.Cpp,".",null,"artifacts",now);run.Start(now);run.Complete(QaRunStatus.Failed,3,1,2,"{}",now.AddSeconds(1));run.Status.Should().Be(QaRunStatus.Failed);run.CriticalFindingCount.Should().Be(1);
 }
 [Fact]public void Finding_ReproductionStatus_IsEvidenceStateNotPersonState()
 {
  var f=new QaFinding(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"project",LanguageId.CSharp,"runtime",Severity.Critical,"dotnet","crash","boom",new string('A',64),null,null,null,null,"NullReferenceException",null,null,DateTimeOffset.UtcNow);f.ApplyReproduction(ReproductionStatus.Reproduced,3,3);f.ReproductionStatus.Should().Be(ReproductionStatus.Reproduced);f.ReproducedAttempts.Should().Be(3);
 }
 [Fact]public void HardwareProfile_RejectsUnusableMemoryBudget()
 {
  var act=()=>new HardwareProfile(Guid.NewGuid(),"low","Mobile",HardwareProfileSource.SimulationTarget,1,1,1,1,1,60,64,30,"{}",DateTimeOffset.UtcNow);act.Should().Throw<ArgumentOutOfRangeException>();
 }
}
