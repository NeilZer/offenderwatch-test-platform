using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace TestPlatform.Api.Infrastructure;

/// <summary>Domain errors that map to clean HTTP responses (RFC 7807 problem details).</summary>
public class NotFoundException(string message) : Exception(message);
public class ConflictException(string message) : Exception(message);
public class BadRequestException(string message) : Exception(message);

/// <summary>Single place that turns exceptions into problem-details responses; never leaks stack traces.</summary>
public class ProblemExceptionHandler(ILogger<ProblemExceptionHandler> logger, IProblemDetailsService problems) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken ct)
    {
        var (status, title) = exception switch
        {
            NotFoundException => (StatusCodes.Status404NotFound, "Not found"),
            ConflictException => (StatusCodes.Status409Conflict, "Conflict"),
            BadRequestException or FormatException => (StatusCodes.Status400BadRequest, "Invalid request"),
            DbUpdateException { InnerException: SqliteException { SqliteErrorCode: 19 } } =>
                (StatusCodes.Status409Conflict, "Rejected by a database rule"),
            _ => (StatusCodes.Status500InternalServerError, "Unexpected error"),
        };
        if (status == 500) logger.LogError(exception, "Unhandled error on {Path}", context.Request.Path);
        else logger.LogInformation("{Status} on {Path}: {Message}", status, context.Request.Path, exception.Message);

        context.Response.StatusCode = status;
        var detail = status == 500 ? "See the server log for details." : (exception.InnerException?.Message ?? exception.Message);
        return await problems.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails = new ProblemDetails { Status = status, Title = title, Detail = detail },
        });
    }
}
