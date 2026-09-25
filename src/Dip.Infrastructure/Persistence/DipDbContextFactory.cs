// Purpose: Implements D ip Db Co nt ex tF ac to ry infrastructure behavior for p er si st en ce.
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Dip.Infrastructure.Persistence;

public sealed class DipDbContextFactory
    : IDesignTimeDbContextFactory<DipDbContext>
{
    public DipDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__PostgreSql");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            connectionString =
                "Host=127.0.0.1;Port=5432;Database=dip;Username=dip;Password=change-me-local-only";
        }

        var options = new DbContextOptionsBuilder<DipDbContext>()
            .UseNpgsql(connectionString)
            .Options;

        return new DipDbContext(options);
    }
}
