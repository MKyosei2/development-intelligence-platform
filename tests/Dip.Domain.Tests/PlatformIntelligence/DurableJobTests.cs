// Purpose: Verifies D ur ab le Jo bT es ts behavior.
using Xunit;
using Dip.Domain.Entities;
using Dip.Domain.Enums;

namespace Dip.Domain.Tests.PlatformIntelligence;

public sealed class DurableJobTests
{
    [Fact]
    public void ExpiredLease_Requeues_WhenAttemptsRemain()
    {
        var now=DateTimeOffset.UtcNow;
        var job=new DurableJob(Guid.NewGuid(),Guid.NewGuid(),DurableJobType.Fuzz,"qa:test","{}",3,now);
        job.Claim("worker-a",now,TimeSpan.FromSeconds(10));

        var recovered=job.RecoverExpiredLease(now.AddSeconds(11));

        Assert.True(recovered);
        Assert.Equal(DurableJobStatus.Queued,job.Status);
        Assert.Null(job.LeaseOwner);
        Assert.Equal(1,job.Attempt);
    }

    [Fact]
    public void Complete_SeparatesExecutionSuccess_FromQualityVerdict()
    {
        var now=DateTimeOffset.UtcNow;
        var job=new DurableJob(Guid.NewGuid(),Guid.NewGuid(),DurableJobType.Benchmark,"benchmark:api","{}",2,now);
        job.Claim("worker-a",now,TimeSpan.FromMinutes(1));
        job.Complete("{\"qualityGate\":\"Failed\"}",now.AddSeconds(1));

        Assert.Equal(DurableJobStatus.Succeeded,job.Status);
        Assert.Equal(100m,job.ProgressPercent);
        Assert.Contains("Failed",job.ResultJson);
    }
}
