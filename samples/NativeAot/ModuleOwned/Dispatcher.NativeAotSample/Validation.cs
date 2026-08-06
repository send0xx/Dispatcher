using FluentValidation;
using Microsoft.AspNetCore.Diagnostics;

namespace Dispatcher.NativeAotSample;

internal sealed class ValidationCommandBehavior<TCommand, TResponse>(
    IEnumerable<IValidator<TCommand>> validators) : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TCommand command,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var context = new ValidationContext<TCommand>(command);
        var results = await Task.WhenAll(validators.Select(validator =>
            validator.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(result => result.Errors)
            .Where(failure => failure is not null)
            .ToArray();

        if (failures.Length > 0)
        {
            throw new ValidationException(failures);
        }

        return await next(cancellationToken);
    }
}

internal sealed class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is not ValidationException validationException)
        {
            return false;
        }

        var errors = validationException.Errors
            .GroupBy(failure => failure.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(failure => failure.ErrorMessage).ToArray());

        httpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
        await Results.ValidationProblem(errors).ExecuteAsync(httpContext);
        return true;
    }
}