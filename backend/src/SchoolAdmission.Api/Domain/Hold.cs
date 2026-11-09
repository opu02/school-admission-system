namespace SchoolAdmission.Api.Domain;

/// <summary>
/// An admission application: a seat held for one student in one class.
/// A hold counts against the class capacity while it is HELD (and not yet past
/// <see cref="ExpiresAt"/>) or CONFIRMED.
/// </summary>
public class Hold
{
    public string Id { get; set; } = default!;

    public string ClassId { get; set; } = default!;

    public SchoolClass Class { get; set; } = default!;

    /// <summary>17-digit birth registration number of the student.</summary>
    public string StudentId { get; set; } = default!;

    public HoldStatus Status { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public DateTimeOffset? CancelledAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// The status a client should see at <paramref name="now"/>. A HELD row whose time
    /// is over is reported as EXPIRED even if the background sweeper has not updated
    /// the row yet, so the API never shows a stale HELD.
    /// </summary>
    public HoldStatus EffectiveStatus(DateTimeOffset now) =>
        Status == HoldStatus.Held && ExpiresAt <= now ? HoldStatus.Expired : Status;
}
