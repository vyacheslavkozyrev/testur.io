namespace Testurio.Api.Middleware;

public sealed class RequestBodyBufferingMiddleware : IMiddleware
{
    public Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        if (context.Request.Path.StartsWithSegments("/v1/webhooks"))
            context.Request.EnableBuffering();
        return next(context);
    }
}
