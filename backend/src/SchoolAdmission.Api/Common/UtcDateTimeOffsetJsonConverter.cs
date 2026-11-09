using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SchoolAdmission.Api.Common;

/// <summary>
/// Writes every <see cref="DateTimeOffset"/> as UTC ISO-8601 with a "Z" suffix,
/// e.g. "2026-11-01T04:10:00Z" or "2026-11-01T04:10:00.123Z" (like the API contract),
/// instead of the default "+00:00" form.
/// </summary>
public sealed class UtcDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetDateTimeOffset().ToUniversalTime();

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        var utc = value.UtcDateTime;
        var format = utc.Ticks % TimeSpan.TicksPerSecond == 0
            ? "yyyy-MM-dd'T'HH:mm:ss'Z'"
            : "yyyy-MM-dd'T'HH:mm:ss.fff'Z'";
        writer.WriteStringValue(utc.ToString(format, CultureInfo.InvariantCulture));
    }
}
