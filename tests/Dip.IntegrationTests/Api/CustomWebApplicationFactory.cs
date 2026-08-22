using Dip.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Dip.IntegrationTests.Api;

public sealed class CustomWebApplicationFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        // Program calls AddInfrastructure(configuration) while the application
        // host is being built. Make the test PostgreSQL connection string
        // available before Program reaches that point.
        builder.UseSetting(
            "ConnectionStrings:PostgreSql",
            connectionString);

        builder.ConfigureServices(services =>
        {
            // Replace every registration added by AddDbContext. Leaving an old
            // registration behind can make tests connect to the development DB.
            services.RemoveAll<DipDbContext>();
            services.RemoveAll<DbContextOptions<DipDbContext>>();

            services.AddDbContext<DipDbContext>(
                options => options.UseNpgsql(connectionString));

            // Program.InitializeDatabaseAsync creates the schema after the
            // final test service provider has been built.
        });
    }
}
