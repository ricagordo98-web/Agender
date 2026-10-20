using FluentValidation;
using FluentValidation.Results;

namespace Agender.Web.Api;

public class ValidationFilter<T>(IValidator<T> validator) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        T? argument = context.Arguments.OfType<T>().FirstOrDefault();

        if (argument is null)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { [typeof(T).Name] = ["The request body is required."] });
        }

        ValidationResult result = await validator.ValidateAsync(argument, context.HttpContext.RequestAborted);

        return result.IsValid
            ? await next(context)
            : Results.ValidationProblem(result.ToDictionary());
    }
}
