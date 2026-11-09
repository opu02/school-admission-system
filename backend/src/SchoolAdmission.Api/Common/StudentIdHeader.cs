using System.Text.RegularExpressions;
using SchoolAdmission.Api.Common.Errors;

namespace SchoolAdmission.Api.Common;

/// <summary>
/// The student's identity: a 17-digit birth registration number sent in the
/// <c>X-Student-Id</c> header (there is no login in this system).
/// </summary>
public static partial class StudentIdHeader
{
    public const string Name = "X-Student-Id";
    public const int Length = 17;

    /// <summary>Validates the raw header value and returns the normalized student id.</summary>
    /// <exception cref="ApiException">400 when the header is missing or not exactly 17 digits.</exception>
    public static string Require(string? rawValue)
    {
        var value = rawValue?.Trim();
        if (string.IsNullOrEmpty(value))
        {
            throw ApiException.BadRequest(
                ErrorCodes.StudentIdRequired,
                "Please enter the student's 17-digit birth registration number first.");
        }

        if (!SeventeenDigits().IsMatch(value))
        {
            throw ApiException.BadRequest(
                ErrorCodes.StudentIdInvalid,
                "The birth registration number must be exactly 17 digits (numbers only).");
        }

        return value;
    }

    [GeneratedRegex("^[0-9]{17}$", RegexOptions.CultureInvariant)]
    private static partial Regex SeventeenDigits();
}
