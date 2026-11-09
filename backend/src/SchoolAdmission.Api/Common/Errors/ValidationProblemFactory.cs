using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace SchoolAdmission.Api.Common.Errors;

/// <summary>
/// Builds the 400 response for invalid request bodies. Raw framework messages
/// (e.g. "The JSON value could not be converted to System.Nullable`1...") are
/// replaced with short, human-readable ones.
/// </summary>
public static class ValidationProblemFactory
{
    public static IActionResult Create(ActionContext context)
    {
        var errors = context.ModelState
            .Where(entry => entry.Value is { Errors.Count: > 0 })
            .GroupBy(entry => NormalizeKey(entry.Key))
            .ToDictionary(
                group => group.Key,
                group => group
                    .SelectMany(entry => entry.Value!.Errors)
                    .Select(error => FriendlyMessage(group.Key, error))
                    .Distinct()
                    .ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid request",
            Detail = string.Join(" ", errors.Values.Select(messages => messages[0])),
        };
        problem.Extensions["code"] = ErrorCodes.ValidationFailed;
        problem.Extensions["traceId"] = context.HttpContext.TraceIdentifier;

        return new BadRequestObjectResult(problem)
        {
            ContentTypes = { "application/problem+json" },
        };
    }

    private static string NormalizeKey(string key)
    {
        // "$.totalSeats" -> "totalSeats"; "" / "request" (whole body) -> "body".
        var trimmed = key.StartsWith("$.", StringComparison.Ordinal) ? key[2..] : key;
        if (trimmed.Length == 0 || trimmed == "$" || trimmed.Equals("request", StringComparison.OrdinalIgnoreCase))
        {
            return "body";
        }

        return char.ToLowerInvariant(trimmed[0]) + trimmed[1..];
    }

    private static string FriendlyMessage(string field, ModelError error)
    {
        if (field == "body")
        {
            return "The request body is missing or is not valid JSON.";
        }

        var isFrameworkMessage =
            error.Exception is not null ||
            string.IsNullOrWhiteSpace(error.ErrorMessage) ||
            error.ErrorMessage.StartsWith("The JSON value", StringComparison.Ordinal) ||
            error.ErrorMessage.Contains("could not be converted", StringComparison.Ordinal);

        return isFrameworkMessage ? $"'{field}' has an invalid value." : error.ErrorMessage;
    }
}
