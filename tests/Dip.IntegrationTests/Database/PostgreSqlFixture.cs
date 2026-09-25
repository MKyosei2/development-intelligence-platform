// Purpose: Defines Postgre SQL Fixture responsibilities for this repository area.
using Testcontainers.PostgreSql;

namespace Dip.IntegrationTests.Database;

public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;

    public async Task InitializeAsync()
    {
        var configuredConnectionString =
            Environment.GetEnvironmentVariable(
                "ConnectionStrings__PostgreSql");

        if (!string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            // CI and the final verification script provide a healthy,
            // externally managed PostgreSQL instance.
            ConnectionString = configuredConnectionString.Trim();
            return;
        }

        // Developer fallback: start an isolated PostgreSQL container.
        _container = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("dip_test")
            .WithUsername("dip")
            .WithPassword("dip-test-password")
            .Build();

        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
    }

    public Task DisposeAsync() =>
        _container is null
            ? Task.CompletedTask
            : _container.DisposeAsync().AsTask();
}
