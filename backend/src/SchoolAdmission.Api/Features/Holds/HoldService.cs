using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Npgsql;
using SchoolAdmission.Api.Common;
using SchoolAdmission.Api.Common.Errors;
using SchoolAdmission.Api.Configuration;
using SchoolAdmission.Api.Data;
using SchoolAdmission.Api.Domain;

namespace SchoolAdmission.Api.Features.Holds;

/// <summary>
/// Seat hold lifecycle: apply (HELD) -> pay (CONFIRMED) / cancel (CANCELLED) / time out (EXPIRED).
/// <para>
/// <b>Overbooking protection.</b> Creating a hold locks the class row with
/// <c>SELECT ... FOR UPDATE</c> inside a transaction. All hold creations for the same
/// class therefore run one after another <i>inside PostgreSQL</i>, so the
/// "count occupied seats, then insert" step can never interleave - not across threads,
/// and not across separate API processes sharing the database.
/// </para>
/// </summary>
public sealed class HoldService(
    AppDbContext db,
    TimeProvider time,
    IOptions<HoldOptions> options,
    ILogger<HoldService> logger)
{
    private readonly HoldOptions _options = options.Value;

    public async Task<HoldResponse> CreateAsync(string classId, string studentId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // 1) Lock the class row. Concurrent requests for this class wait here.
        var schoolClass = (await db.Classes
                .FromSql($"SELECT * FROM classes WHERE id = {classId} FOR UPDATE")
                .AsNoTracking()
                .ToListAsync(cancellationToken))
            .SingleOrDefault()
            ?? throw ClassNotFound();

        // Read the clock only after the lock: a request that waited in the queue must not use an old time.
        var now = time.GetUtcNowMs();

        // 2) Release this class's holds whose time is over (keeps counts and the unique index clean).
        await ExpireOverdueHoldsAsync(db.Holds.Where(h => h.ClassId == classId), now, cancellationToken);

        // 3) One active application per student per class.
        var existing = await db.Holds
            .AsNoTracking()
            .Where(h => h.ClassId == classId
                        && h.StudentId == studentId
                        && (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed))
            .Select(h => new { h.Id, h.Status })
            .FirstOrDefaultAsync(cancellationToken);

        if (existing is not null)
        {
            throw ActiveHoldExists(existing.Id, existing.Status);
        }

        // 4) Capacity check: held + confirmed must stay <= totalSeats.
        var occupiedSeats = await db.Holds.CountAsync(
            h => h.ClassId == classId && (h.Status == HoldStatus.Held || h.Status == HoldStatus.Confirmed),
            cancellationToken);

        if (occupiedSeats >= schoolClass.TotalSeats)
        {
            throw ApiException.Conflict(
                ErrorCodes.NoSeatsAvailable,
                $"Sorry, there are no free seats in \"{schoolClass.Name}\" right now. " +
                "A seat may open up again if another applicant does not pay in time.");
        }

        // 5) Take the seat.
        var hold = new Hold
        {
            Id = IdGenerator.NewHoldId(),
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.Held,
            CreatedAt = now,
            ExpiresAt = (now + _options.Duration).TruncateToSeconds(),
            UpdatedAt = now,
        };
        db.Holds.Add(hold);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Safety net: the partial unique index rejected a second active hold.
            throw ActiveHoldExists(existingHoldId: null, HoldStatus.Held);
        }

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Hold {HoldId} created for class {ClassId}, expires at {ExpiresAt:o}",
            hold.Id, classId, hold.ExpiresAt);

        return ToResponse(hold, schoolClass.Name, now);
    }

    public async Task<HoldResponse> GetAsync(string holdId, string? studentId, CancellationToken cancellationToken)
    {
        var row = await db.Holds
            .AsNoTracking()
            .Where(h => h.Id == holdId)
            .Select(h => new { Hold = h, ClassName = h.Class.Name })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw HoldNotFound();

        // The header is optional here; if it is sent, it must belong to the hold's student.
        if (studentId is not null && row.Hold.StudentId != studentId)
        {
            throw HoldNotOwned();
        }

        return ToResponse(row.Hold, row.ClassName, time.GetUtcNowMs());
    }

    public async Task<IReadOnlyList<HoldResponse>> ListForStudentAsync(string studentId, CancellationToken cancellationToken)
    {
        var rows = await db.Holds
            .AsNoTracking()
            .Where(h => h.StudentId == studentId)
            .OrderByDescending(h => h.CreatedAt)
            .Select(h => new { Hold = h, ClassName = h.Class.Name })
            .ToListAsync(cancellationToken);

        var now = time.GetUtcNowMs();
        return rows.Select(r => ToResponse(r.Hold, r.ClassName, now)).ToList();
    }

    /// <summary>
    /// Pays the admission fee. Idempotent: confirming an already CONFIRMED hold returns it
    /// unchanged, so <c>confirmedSeats</c> can only go up once per hold.
    /// </summary>
    public async Task<HoldResponse> ConfirmAsync(string holdId, string studentId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var hold = await LockHoldAsync(holdId, cancellationToken);
        var now = time.GetUtcNowMs();
        if (hold.StudentId != studentId)
        {
            throw HoldNotOwned();
        }

        switch (hold.EffectiveStatus(now))
        {
            case HoldStatus.Confirmed:
                break; // Already paid: nothing changes.

            case HoldStatus.Cancelled:
                throw ApiException.Conflict(
                    ErrorCodes.HoldCancelled,
                    "This application was cancelled, so the fee cannot be paid. Please apply again.");

            case HoldStatus.Expired:
                await MarkExpiredIfNeededAsync(hold, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                throw HoldExpired();

            case HoldStatus.Held:
                hold.Status = HoldStatus.Confirmed;
                hold.ConfirmedAt = now;
                hold.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Hold {HoldId} confirmed", hold.Id);
                break;
        }

        await transaction.CommitAsync(cancellationToken);
        return ToResponse(hold, await GetClassNameAsync(hold.ClassId, cancellationToken), now);
    }

    /// <summary>
    /// Cancels an unpaid application and frees its seat. Cancelling twice is allowed (204).
    /// A paid (CONFIRMED) admission cannot be cancelled online.
    /// </summary>
    public async Task CancelAsync(string holdId, string studentId, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        var hold = await LockHoldAsync(holdId, cancellationToken);
        var now = time.GetUtcNowMs();
        if (hold.StudentId != studentId)
        {
            throw HoldNotOwned();
        }

        switch (hold.EffectiveStatus(now))
        {
            case HoldStatus.Cancelled:
                break; // Already cancelled: idempotent.

            case HoldStatus.Confirmed:
                throw ApiException.Conflict(
                    ErrorCodes.HoldAlreadyConfirmed,
                    "The admission fee is already paid, so this admission cannot be cancelled online. " +
                    "Please contact the school office.");

            case HoldStatus.Expired:
                await MarkExpiredIfNeededAsync(hold, now, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                throw ApiException.Conflict(
                    ErrorCodes.HoldExpired,
                    "This application has already expired and its seat was released. There is nothing to cancel.");

            case HoldStatus.Held:
                hold.Status = HoldStatus.Cancelled;
                hold.CancelledAt = now;
                hold.UpdatedAt = now;
                await db.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Hold {HoldId} cancelled", hold.Id);
                break;
        }

        await transaction.CommitAsync(cancellationToken);
    }

    /// <summary>Marks every overdue HELD row in <paramref name="scope"/> as EXPIRED. Returns the number of rows changed.</summary>
    public static Task<int> ExpireOverdueHoldsAsync(IQueryable<Hold> scope, DateTimeOffset now, CancellationToken cancellationToken) =>
        scope
            .Where(h => h.Status == HoldStatus.Held && h.ExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(h => h.Status, HoldStatus.Expired)
                    .SetProperty(h => h.UpdatedAt, now),
                cancellationToken);

    private async Task<Hold> LockHoldAsync(string holdId, CancellationToken cancellationToken) =>
        (await db.Holds
            .FromSql($"SELECT * FROM holds WHERE id = {holdId} FOR UPDATE")
            .ToListAsync(cancellationToken))
        .SingleOrDefault()
        ?? throw HoldNotFound();

    private async Task MarkExpiredIfNeededAsync(Hold hold, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (hold.Status != HoldStatus.Held)
        {
            return;
        }

        hold.Status = HoldStatus.Expired;
        hold.UpdatedAt = now;
        await db.SaveChangesAsync(cancellationToken);
    }

    private Task<string> GetClassNameAsync(string classId, CancellationToken cancellationToken) =>
        db.Classes.Where(c => c.Id == classId).Select(c => c.Name).SingleAsync(cancellationToken);

    private static HoldResponse ToResponse(Hold hold, string className, DateTimeOffset now) =>
        new(
            hold.Id,
            hold.ClassId,
            className,
            hold.StudentId,
            hold.EffectiveStatus(now).ToApiString(),
            hold.ExpiresAt,
            hold.CreatedAt,
            hold.ConfirmedAt,
            hold.CancelledAt,
            now);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };

    private static ApiException ClassNotFound() =>
        ApiException.NotFound(ErrorCodes.ClassNotFound, "This class does not exist.");

    private static ApiException HoldNotFound() =>
        ApiException.NotFound(ErrorCodes.HoldNotFound, "This application does not exist.");

    private static ApiException HoldNotOwned() =>
        ApiException.Forbidden(ErrorCodes.HoldNotOwned, "This application belongs to a different student.");

    private static ApiException HoldExpired() =>
        ApiException.Conflict(
            ErrorCodes.HoldExpired,
            "The time to pay has run out and the seat was released. Please apply again if seats are still available.");

    private static ApiException ActiveHoldExists(string? existingHoldId, HoldStatus existingStatus) =>
        new(StatusCodes.Status409Conflict,
            ErrorCodes.ActiveHoldExists,
            "Conflict",
            existingStatus == HoldStatus.Confirmed
                ? "This student is already admitted to this class."
                : "This student already has an active application for this class. Please pay the fee or cancel it first.")
        {
            Extra = existingHoldId is null ? null : new Dictionary<string, object?> { ["holdId"] = existingHoldId },
        };
}
