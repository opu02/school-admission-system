namespace SchoolAdmission.Api.Configuration;

/// <summary>Settings for seat holds. Bound from flat environment variables.</summary>
public sealed class HoldOptions
{
    public const int DefaultDurationSeconds = 600;
    public const int DefaultSweepIntervalSeconds = 5;

    /// <summary>HOLD_DURATION_SECONDS: how long a seat stays reserved before it is released.</summary>
    public int DurationSeconds { get; set; } = DefaultDurationSeconds;

    /// <summary>HOLD_EXPIRY_SWEEP_SECONDS: how often the background job marks old holds as EXPIRED.</summary>
    public int SweepIntervalSeconds { get; set; } = DefaultSweepIntervalSeconds;

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);

    public TimeSpan SweepInterval => TimeSpan.FromSeconds(SweepIntervalSeconds);
}
