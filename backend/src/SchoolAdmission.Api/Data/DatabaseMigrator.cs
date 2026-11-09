using Microsoft.EntityFrameworkCore;

namespace SchoolAdmission.Api.Data;

/// <summary>
/// Applies EF Core migrations on startup.
/// <para>
/// A PostgreSQL advisory lock makes this safe when several API processes start at
/// the same time against the same database (only one of them migrates; the others
/// wait and then find nothing to do).
/// </para>
/// </summary>
public static class DatabaseMigrator
{
    // Any constant 64-bit number; it only has to be unique among this app's advisory locks.
    private const long MigrationLockKey = 7_243_110_001;
    private const int MaxConnectAttempts = 15;
    private static readonly TimeSpan RetryDelay = TimeSpan.FromSeconds(2);

    public static async Task MigrateAsync(IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger(typeof(DatabaseMigrator).FullName!);

        await OpenConnectionWithRetryAsync(db, logger, cancellationToken);
        try
        {
            await db.Database.ExecuteSqlRawAsync(
                "SELECT pg_advisory_lock({0})", new object[] { MigrationLockKey }, cancellationToken);
            try
            {
                var pending = (await db.Database.GetPendingMigrationsAsync(cancellationToken)).ToList();
                if (pending.Count == 0)
                {
                    logger.LogInformation("Database schema is up to date");
                    return;
                }

                logger.LogInformation("Applying {Count} migration(s): {Migrations}", pending.Count, string.Join(", ", pending));
                await db.Database.MigrateAsync(cancellationToken);
                logger.LogInformation("Database migrations applied");
            }
            finally
            {
                await db.Database.ExecuteSqlRawAsync(
                    "SELECT pg_advisory_unlock({0})", new object[] { MigrationLockKey }, CancellationToken.None);
            }
        }
        finally
        {
            await db.Database.CloseConnectionAsync();
        }
    }

    private static async Task OpenConnectionWithRetryAsync(AppDbContext db, ILogger logger, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.OpenConnectionAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < MaxConnectAttempts && !cancellationToken.IsCancellationRequested)
            {
                logger.LogWarning(
                    "Database is not reachable yet (attempt {Attempt}/{Max}): {Message}. Retrying in {Delay}s",
                    attempt, MaxConnectAttempts, ex.Message, RetryDelay.TotalSeconds);
                await Task.Delay(RetryDelay, cancellationToken);
            }
        }
    }
}
