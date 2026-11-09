using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace SchoolAdmission.Api.Common.Errors;

/// <summary>
/// Converts every exception into a consistent problem+json response:
/// <c>{ "type", "title", "status", "detail", "code", "traceId" }</c>.
/// Unexpected errors never leak internal details to the client.
/// </summary>
public sealed class ApiExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<ApiExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var (status, code, title, detail) = exception switch
        {
            ApiException api => (api.StatusCode, api.Code, api.Title, api.Message),
            BadHttpRequestException => (
                StatusCodes.Status400BadRequest,
                ErrorCodes.BadRequest,
                "Invalid request",
                "The request could not be read. Please check the data you sent."),
            _ => (
                StatusCodes.Status500InternalServerError,
                ErrorCodes.InternalError,
                "Server error",
                "Something went wrong on the server. Please try again in a moment."),
        };

        if (status >= StatusCodes.Status500InternalServerError)
        {
            logger.LogError(exception, "Unhandled exception for {Method} {Path}",
                httpContext.Request.Method, httpContext.Request.Path);
        }

        httpContext.Response.StatusCode = status;

        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
        };
        problem.Extensions["code"] = code;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problem,
        });
    }
}
