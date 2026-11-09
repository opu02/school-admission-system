namespace SchoolAdmission.Api.Common;

public static class TimeProviderExtensions
{
    /// <summary>
    /// Current UTC time truncated to whole milliseconds. PostgreSQL stores microseconds
    /// and .NET uses 100ns ticks; truncating keeps values identical before and after a
    /// database round-trip.
    /// </summary>
    public static DateTimeOffset GetUtcNowMs(this TimeProvider time)
    {
        var now = time.GetUtcNow();
        return new DateTimeOffset(now.Ticks - now.Ticks % TimeSpan.TicksPerMillisecond, TimeSpan.Zero);
    }

    /// <summary>Truncates to whole seconds (used for <c>expiresAt</c>, matching the API examples).</summary>
    public static DateTimeOffset TruncateToSeconds(this DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        return new DateTimeOffset(utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond, TimeSpan.Zero);
    }
}
