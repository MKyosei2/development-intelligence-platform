using Dip.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Dip.Infrastructure.Tests.Persistence;

public sealed class MigrationMetadataTests
{
    [Fact]
    public void FormalMigrationChain_IsDiscoverable_AndModelBuilds()
    {
        var options=new DbContextOptionsBuilder<DipDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=dip_metadata_test;Username=dip;Password=dip")
            .Options;
        using var db=new DipDbContext(options);

        var migrations=db.Database.GetMigrations().ToArray();
        var entities=db.Model.GetEntityTypes().Select(x=>x.ClrType.Name).ToHashSet(StringComparer.Ordinal);

        Assert.Contains("202608120001_InitialBaseline",migrations);
        Assert.Contains("202608120002_AddDurablePlatformIntelligence",migrations);
        Assert.Contains("DurableJob",entities);
        Assert.Contains("ArtifactRecord",entities);
        Assert.Contains("Incident",entities);
        Assert.Contains("BenchmarkRun",entities);
    }
}
