namespace Testurio.Api.Middleware;

public sealed class RequestBodyBufferingMiddleware : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments(WebhookRouteConstants.BufferingPathPrefix))
            context.Request.EnableBuffering();
        return next(context);
    }
}
