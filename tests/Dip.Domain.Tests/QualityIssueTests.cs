// Purpose: Verifies behavior and invariants covered by Q ua li ty Is su eT es ts.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Xunit;
namespace Dip.Domain.Tests;
public sealed class QualityIssueTests
{
    [Fact] public void Reoccur_IncrementsCount()
    {
        var now=DateTimeOffset.UtcNow;var i=new QualityIssue(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),
            new string('A',64),"performance",Severity.Warning,"FPS regression","FPS decreased.",
            "benchmark","fps-rule",null,now);
        i.Reoccur(Guid.NewGuid(),Severity.Critical,now.AddMinutes(1));
        Assert.Equal(2,i.OccurrenceCount);Assert.Equal(Severity.Critical,i.Severity);
    }
}
