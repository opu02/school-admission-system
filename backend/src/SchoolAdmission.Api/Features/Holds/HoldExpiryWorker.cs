using Microsoft.Extensions.Options;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Data;

namespace SchoolAdmission.Api.Features.Holds;

/// <summary>
/// Every few seconds, marks HELD holds whose time is over as EXPIRED - no API call needed.
/// <para>
/// Seat counts never depend on this job: they already ignore overdue HELD rows. The job
/// only makes the stored status match reality. Running it in several API processes at
/// once is harmless (the UPDATE is idempotent).
/// </para>
/// </summary>
public sealed class HoldExpiryWorker(
    IServiceScopeFactory scopeFactory,
    TimeProvider time,
    IOptions<HoldOptions> options,
    ILogger<HoldExpiryWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.SweepInterval, time);

        do
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                var expired = await HoldService.ExpireOverdueHoldsAsync(db.Holds, time.GetUtcNowMs(), stoppingToken);
                if (expired > 0)
                {
                    logger.LogInformation("Expired {Count} overdue hold(s)", expired);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                // e.g. database briefly unavailable - try again on the next tick.
                logger.LogWarning(ex, "Hold expiry sweep failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
