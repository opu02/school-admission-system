namespace SchoolAdmission.Api.Domain;

/// <summary>Creates opaque, prefixed, non-guessable identifiers (e.g. "c_3f2a...", "h_9b1c...").</summary>
public static class IdGenerator
{
    public const string ClassPrefix = "c_";
    public const string HoldPrefix = "h_";

    public static string NewClassId() => ClassPrefix + Guid.NewGuid().ToString("N");

    public static string NewHoldId() => HoldPrefix + Guid.NewGuid().ToString("N");
}
