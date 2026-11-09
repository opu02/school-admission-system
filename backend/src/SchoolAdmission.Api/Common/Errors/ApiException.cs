namespace SchoolAdmission.Api.Common.Errors;

/// <summary>
/// An expected business error. It is turned into an RFC 7807 problem response by
/// <see cref="ApiExceptionHandler"/> with the given status code, code and message.
/// </summary>
public sealed class ApiException : Exception
{
    public ApiException(int statusCode, string code, string title, string message)
        : base(message)
    {
        StatusCode = statusCode;
        Code = code;
        Title = title;
    }

    public int StatusCode { get; }

    public string Code { get; }

    public string Title { get; }

    /// <summary>Optional extra fields added to the problem response (e.g. the id of an existing hold).</summary>
    public IReadOnlyDictionary<string, object?>? Extra { get; init; }

    public static ApiException BadRequest(string code, string message) =>
        new(StatusCodes.Status400BadRequest, code, "Invalid request", message);

    public static ApiException Forbidden(string code, string message) =>
        new(StatusCodes.Status403Forbidden, code, "Not allowed", message);

    public static ApiException NotFound(string code, string message) =>
        new(StatusCodes.Status404NotFound, code, "Not found", message);

    public static ApiException Conflict(string code, string message) =>
        new(StatusCodes.Status409Conflict, code, "Conflict", message);
}
