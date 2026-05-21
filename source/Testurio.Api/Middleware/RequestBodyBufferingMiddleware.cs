namespace Testurio.Api.Middleware;

public sealed class RequestBodyBufferingMiddleware : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        var path = context.Request.Path.Value ?? string.Empty;
        // Enable buffering for both /webhooks/* and /v1/webhooks/* paths
        if (path.Contains("/webhooks/", StringComparison.OrdinalIgnoreCase))
            context.Request.EnableBuffering();
        return next(context);
    }
}
