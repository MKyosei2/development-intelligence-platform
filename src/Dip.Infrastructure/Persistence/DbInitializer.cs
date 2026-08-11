using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Dip.Infrastructure.Persistence;

public static class DbInitializer
{
    private const int MaximumAttempts = 10;

    public static async Task InitializeDatabaseAsync(this IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(serviceProvider);
        var logger = serviceProvider.GetRequiredService<ILogger<DipDbContext>>();

        for (var attempt = 1; attempt <= MaximumAttempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                await using var scope = serviceProvider.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<DipDbContext>();
                var adopter = scope.ServiceProvider.GetRequiredService<LegacyMigrationAdopter>();

                await adopter.AdoptIfNecessaryAsync(dbContext, cancellationToken);
                await dbContext.Database.MigrateAsync(cancellationToken);

                logger.LogInformation("Database is on the formal EF Core migration chain.");
                return;
            }
            catch (LegacySchemaMismatchException)
            {
                // Fail closed: schema mismatches require operator review, not blind retries/mutation.
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception) when (attempt < MaximumAttempts)
            {
                var delay = TimeSpan.FromSeconds(Math.Min(10, attempt));
                logger.LogWarning(exception,
                    "Database initialization attempt {Attempt}/{MaximumAttempts} failed. Retrying in {DelaySeconds} seconds.",
                    attempt, MaximumAttempts, delay.TotalSeconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }
}
