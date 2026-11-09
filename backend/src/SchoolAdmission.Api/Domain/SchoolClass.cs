namespace SchoolAdmission.Api.Domain;

/// <summary>
/// One admission unit with its own seat pool, e.g. "Class 6 - Morning Shift".
/// Every shift of a grade is modelled as a separate class.
/// </summary>
public class SchoolClass
{
    public const int NameMaxLength = 100;
    public const int MaxTotalSeats = 100_000;

    public string Id { get; set; } = default!;

    public string Name { get; set; } = default!;

    /// <summary>Upper-cased, whitespace-collapsed name used to keep class names unique.</summary>
    public string NormalizedName { get; set; } = default!;

    public int TotalSeats { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ICollection<Hold> Holds { get; } = new List<Hold>();

    public static string NormalizeName(string name) =>
        string.Join(' ', name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
              .ToUpperInvariant();
}
