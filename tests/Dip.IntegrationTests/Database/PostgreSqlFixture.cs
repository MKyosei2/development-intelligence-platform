using Testcontainers.PostgreSql;

namespace Dip.IntegrationTests.Database;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    public PostgreSqlContainer Container { get; } =
        new PostgreSqlBuilder()
            .WithImage("postgres:16")
            .WithDatabase("dip_test")
            .WithUsername("dip")
            .WithPassword("dip-test-password")
            .Build();

    public string ConnectionString => Container.GetConnectionString();

    public Task InitializeAsync() => Container.StartAsync();

    public Task DisposeAsync() => Container.DisposeAsync().AsTask();
}
