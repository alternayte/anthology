using FluentValidation;

namespace Anthology.Kernel;

public sealed class ValidationFilter<T> : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var validator = context.HttpContext.RequestServices.GetService<IValidator<T>>();
        if (validator is null)
            return await next(context);

        var input = context.Arguments.OfType<T>().FirstOrDefault();
        if (input is null)
            return await next(context);

        var result = await validator.ValidateAsync(input);
        return result.IsValid
            ? await next(context)
            : TypedResults.ValidationProblem(result.ToDictionary());
    }
}
