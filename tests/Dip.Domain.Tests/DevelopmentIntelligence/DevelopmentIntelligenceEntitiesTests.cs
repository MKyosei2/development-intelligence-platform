// Purpose: Verifies D ev el op me nt In te ll ig en ce En ti ti es Te st s behavior.
using Dip.Domain.Entities;
using Dip.Domain.Enums;
using Xunit;

namespace Dip.Domain.Tests.DevelopmentIntelligence;

public sealed class DevelopmentIntelligenceEntitiesTests
{
    [Fact]
    public void ReplaySession_ClampsDurationAndStartsEnabled()
    {
        var session=new GameReplaySession(Guid.NewGuid(),Guid.NewGuid(),"boss-01","Boss","BossScene",Guid.NewGuid().ToString(),123,100000,"Game.exe","",null,null,DateTimeOffset.UtcNow);
        Assert.Equal(86400,session.DurationSeconds);
        Assert.True(session.Enabled);
    }

    [Fact]
    public void HardwareBacktest_ComputesSignedPredictionError()
    {
        var item=new HardwareBacktest(Guid.NewGuid(),Guid.NewGuid(),Guid.NewGuid(),"boss","abc",60m,54m,"gpu:test",DateTimeOffset.UtcNow);
        Assert.Equal(-10m,item.PredictionErrorPercent);
    }

    [Fact]
    public void RepositoryDependency_RejectsSelfDependency()
    {
        var repo=Guid.NewGuid();
        Assert.Throws<ArgumentException>(()=>new RepositoryDependency(Guid.NewGuid(),Guid.NewGuid(),repo,repo,RepositoryDependencyKind.Runtime,"runtime",true,DateTimeOffset.UtcNow));
    }

    [Fact]
    public void ReleaseReadiness_ClampsConfidence()
    {
        var snapshot=new ReleaseReadinessSnapshot(Guid.NewGuid(),Guid.NewGuid(),"abc",ReleaseDecision.ConditionalGo,120m,"[]","[]","[]","review",DateTimeOffset.UtcNow);
        Assert.Equal(100m,snapshot.Confidence);
    }

    [Fact]
    public void GlobalPerformanceBudget_AllowsNullProjectScope()
    {
        var profile=new PerformanceBudgetProfile(Guid.NewGuid(),null,"vr-90","VR 90","VR","90Hz","[]",true,DateTimeOffset.UtcNow);
        Assert.Null(profile.ProjectId);
        Assert.Equal("vr-90",profile.Key);
    }
}
