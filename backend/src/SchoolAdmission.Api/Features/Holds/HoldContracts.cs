namespace SchoolAdmission.Api.Features.Holds;

/// <summary>
/// An admission application (seat hold) as returned by every holds endpoint.
/// Required contract fields: holdId, classId, studentId, status, expiresAt (and className
/// for <c>GET /me/holds</c>). The other fields are additions that help the UI.
/// </summary>
/// <param name="HoldId">Hold id, e.g. "h_9b1c...".</param>
/// <param name="ClassId">Class the seat belongs to.</param>
/// <param name="ClassName">Display name of the class.</param>
/// <param name="StudentId">17-digit birth registration number.</param>
/// <param name="Status">HELD, CONFIRMED, EXPIRED or CANCELLED.</param>
/// <param name="ExpiresAt">When an unpaid hold releases its seat (UTC).</param>
/// <param name="CreatedAt">When the application was made (UTC).</param>
/// <param name="ConfirmedAt">When the fee was paid, if confirmed.</param>
/// <param name="CancelledAt">When the application was cancelled, if cancelled.</param>
/// <param name="ServerTime">
/// Server clock at response time. The UI uses <c>expiresAt - serverTime</c> for its
/// countdown so a wrong clock on the user's computer does not matter.
/// </param>
public sealed record HoldResponse(
    string HoldId,
    string ClassId,
    string ClassName,
    string StudentId,
    string Status,
    DateTimeOffset ExpiresAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset ServerTime);
