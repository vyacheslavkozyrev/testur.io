using Microsoft.AspNetCore.Diagnostics;
using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.Middleware;

internal sealed class GlobalExceptionHandler(IProblemDetailsService pds) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx,
        Exception ex,
        CancellationToken ct)
    {
        var (status, title) = ex switch
        {
            ValidationException v => (StatusCodes.Status400BadRequest, v.Message),
            UnauthorizedAccessException => (StatusCodes.Status401Unauthorized, "Unauthorized"),
            BadHttpRequestException bhr => (bhr.StatusCode, "Invalid request"),
            _ => (StatusCodes.Status500InternalServerError, "An unexpected error occurred")
        };

        ctx.Response.StatusCode = status;
        return await pds.TryWriteAsync(new()
        {
            HttpContext = ctx,
            ProblemDetails = { Status = status, Title = title }
        });
    }
}
