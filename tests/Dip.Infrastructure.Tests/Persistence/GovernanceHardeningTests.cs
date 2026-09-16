// Purpose: Verifies G ov er na nc eH ar de ni ng Te st s behavior.
using Dip.Application.Features.Authorization;
using Dip.Infrastructure.Persistence;
using Dip.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Tests.Persistence;

public sealed class GovernanceHardeningTests
{
    [Fact]
    public async Task GovernanceExecution_IsExactlyOnce_AndRejectsEvidenceMutation()
    {
        var options=new DbContextOptionsBuilder<DipDbContext>().UseInMemoryDatabase($"gov-{Guid.NewGuid():N}").Options;
        await using var db=new DipDbContext(options);
        var coordinator=new QaGovernanceExecutionCoordinator(db);
        var projectId=Guid.NewGuid();var runId=Guid.NewGuid();var calls=0;
        var first=await coordinator.ExecuteOnceAsync(projectId,runId,"2.0.0",new string('a',64),_=>{calls++;return Task.FromResult(new string('b',64));},default);
        var duplicate=await coordinator.ExecuteOnceAsync(projectId,runId,"2.0.0",new string('a',64),_=>{calls++;return Task.FromResult(new string('c',64));},default);
        Assert.True(first);Assert.False(duplicate);Assert.Equal(1,calls);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>coordinator.ExecuteOnceAsync(projectId,runId,"2.0.0",new string('d',64),_=>Task.FromResult(new string('e',64)),default));
        await Assert.ThrowsAsync<InvalidOperationException>(()=>coordinator.ExecuteOnceAsync(Guid.NewGuid(),runId,"2.0.0",new string('a',64),_=>Task.FromResult(new string('e',64)),default));
    }

    [Fact]
    public async Task ProjectResourceResolver_ProtectsArtifactByOwningProject()
    {
        var options=new DbContextOptionsBuilder<DipDbContext>().UseInMemoryDatabase($"resolver-{Guid.NewGuid():N}").Options;
        await using var db=new DipDbContext(options);
        var projectId=Guid.NewGuid();var artifactId=Guid.NewGuid();
        db.ArtifactRecords.Add(new ArtifactRecord(artifactId,projectId,Guid.NewGuid(),"qa-log","proof.txt","standard",null,null,null,null,DateTimeOffset.UtcNow,null));
        await db.SaveChangesAsync();
        var resolver=new ProjectResourceProjectResolver(db);
        Assert.Equal(projectId,await resolver.ResolveProjectIdAsync(ProjectResourceKind.Artifact,artifactId,default));
    }
}
