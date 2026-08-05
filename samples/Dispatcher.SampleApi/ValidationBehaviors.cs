using FluentValidation;

namespace Dispatcher.SampleApi;

internal sealed class ValidationCommandBehavior<TCommand, TResponse>(
    IEnumerable<IValidator<TCommand>> validators) : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public async ValueTask<TResponse> HandleAsync(
        TCommand command,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        await ValidateAsync(command, cancellationToken);
        return await next(cancellationToken);
    }

    private async ValueTask ValidateAsync(TCommand command, CancellationToken cancellationToken)
    {
        if (!validators.Any())
        {
            return;
        }

        var context = new ValidationContext<TCommand>(command);
        var results = await Task.WhenAll(validators.Select(validator =>
            validator.ValidateAsync(context, cancellationToken)));
        var failures = results.SelectMany(result => result.Errors).Where(failure => failure is not null).ToArray();

        if (failures.Length > 0)
        {
            throw new ValidationException(failures);
        }
    }
}