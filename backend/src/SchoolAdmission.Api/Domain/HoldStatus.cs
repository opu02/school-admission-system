namespace SchoolAdmission.Api.Domain;

/// <summary>Lifecycle of an admission application (a seat hold).</summary>
public enum HoldStatus
{
    /// <summary>Seat is reserved for the student until <see cref="Hold.ExpiresAt"/>.</summary>
    Held,

    /// <summary>Admission fee paid; the seat is permanently taken.</summary>
    Confirmed,

    /// <summary>Fee was not paid in time; the seat went back to the pool.</summary>
    Expired,

    /// <summary>The parent withdrew the application; the seat went back to the pool.</summary>
    Cancelled,
}

public static class HoldStatusExtensions
{
    /// <summary>The exact string used in the API contract and in the database ("HELD", ...).</summary>
    public static string ToApiString(this HoldStatus status) => status switch
    {
        HoldStatus.Held => "HELD",
        HoldStatus.Confirmed => "CONFIRMED",
        HoldStatus.Expired => "EXPIRED",
        HoldStatus.Cancelled => "CANCELLED",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown hold status."),
    };

    public static HoldStatus FromApiString(string value) => value switch
    {
        "HELD" => HoldStatus.Held,
        "CONFIRMED" => HoldStatus.Confirmed,
        "EXPIRED" => HoldStatus.Expired,
        "CANCELLED" => HoldStatus.Cancelled,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown hold status."),
    };
}
