using Microsoft.AspNetCore.Diagnostics;
using System.ComponentModel.DataAnnotations;
using Testurio.Core.Exceptions;

namespace Testurio.Api.Middleware;

internal sealed class GlobalExceptionHandler(IProblemDetailsService pds) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext ctx,
        Exception ex,
        CancellationToken ct)
    {
        if (ex is PlanLimitExceededException planLimit)
        {
            ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
            return await pds.TryWriteAsync(new()
            {
                HttpContext = ctx,
                ProblemDetails =
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title  = "Plan limit reached",
                    Detail = planLimit.Message,
                    Extensions =
                    {
                        ["limitName"]    = planLimit.LimitName,
                        ["requiredPlan"] = planLimit.RequiredPlan,
                    }
                }
            });
        }

        var (status, title) = ex switch
        {
            ValidationException v => (StatusCodes.Status400BadRequest, v.Message),
            NotFoundException n => (StatusCodes.Status404NotFound, n.Message),
            ConflictException c => (StatusCodes.Status409Conflict, c.Message),
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
