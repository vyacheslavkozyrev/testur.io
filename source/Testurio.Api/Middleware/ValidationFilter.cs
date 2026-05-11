using System.ComponentModel.DataAnnotations;

namespace Testurio.Api.Middleware;

/// <summary>
/// Endpoint filter that validates request body objects annotated with DataAnnotations attributes.
/// Returns 400 with a ValidationProblemDetails body if any required fields are missing or invalid.
/// </summary>
internal sealed class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext ctx, EndpointFilterDelegate next)
    {
        var target = ctx.Arguments.OfType<T>().FirstOrDefault();
        if (target is not null)
        {
            var errors = new List<ValidationResult>();
            if (!Validator.TryValidateObject(target, new ValidationContext(target), errors, validateAllProperties: true))
            {
                var problemErrors = errors
                    .GroupBy(e => string.Join(",", e.MemberNames))
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(e => e.ErrorMessage ?? "Invalid value.").ToArray());

                return TypedResults.ValidationProblem(problemErrors);
            }
        }

        return await next(ctx);
    }
}
