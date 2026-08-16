namespace Dispatcher.DependencyInjection.Tests.TestSupport;

internal sealed class FirstGreetingBehavior(TestState state) : IPipelineBehavior<GreetingQuery, string>
{
    public async ValueTask<string> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        state.Record("first-before");
        var result = await next(cancellationToken);
        state.Record("first-after");
        return result;
    }
}

internal sealed class SecondGreetingBehavior(TestState state) : IPipelineBehavior<GreetingQuery, string>
{
    public async ValueTask<string> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        state.Record("second-before");
        var result = await next(cancellationToken);
        state.Record("second-after");
        return result;
    }
}

internal sealed class ShortCircuitSumBehavior : IPipelineBehavior<SumCommand, int>
{
    public ValueTask<int> HandleAsync(
        SumCommand command,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken) => ValueTask.FromResult(42);
}

internal sealed class TokenReplacingBehavior(CancellationToken replacement)
    : IPipelineBehavior<TokenQuery, CancellationToken>
{
    public ValueTask<CancellationToken> HandleAsync(
        TokenQuery query,
        RequestHandlerDelegate<CancellationToken> next,
        CancellationToken cancellationToken) => next(replacement);
}

internal sealed class RecordCommandBehavior(TestState state) : IPipelineBehavior<RecordCommand, Unit>
{
    public async ValueTask<Unit> HandleAsync(
        RecordCommand command,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken)
    {
        state.Record("record-before");
        var result = await next(cancellationToken);
        state.Record("record-after");
        return result;
    }
}

internal sealed class CommandBaseBehavior<TCommand, TResponse>(TestState state)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommandBase
{
    public ValueTask<TResponse> HandleAsync(
        TCommand command,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        state.Record("command-base");
        return next(cancellationToken);
    }
}

internal sealed class ResponseCommandBehavior<TCommand, TResponse>(TestState state)
    : IPipelineBehavior<TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    public ValueTask<TResponse> HandleAsync(
        TCommand command,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        state.Record("response-command");
        return next(cancellationToken);
    }
}

internal sealed class PassthroughGreetingBehavior : IPipelineBehavior<GreetingQuery, string>
{
    public ValueTask<string> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

internal sealed class TransientGreetingBehavior : IPipelineBehavior<GreetingQuery, string>
{
    public TransientGreetingBehavior(TestState state)
    {
        state.BehaviorInstances++;
    }

    public ValueTask<string> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

internal sealed class BaseGreetingBehavior(TestState state) : IPipelineBehavior<BaseGreetingQuery, string>
{
    public async ValueTask<string> HandleAsync(
        BaseGreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        state.Record("base-before");
        var result = await next(cancellationToken);
        state.Record("base-after");
        return result;
    }
}

internal sealed class PartiallyClosedGreetingBehavior<TResponse>
    : IPipelineBehavior<GreetingQuery, TResponse>
{
    public ValueTask<TResponse> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

internal sealed class FixedResponseBehavior<TRequest> : IPipelineBehavior<TRequest, string>
    where TRequest : IRequest
{
    public ValueTask<string> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

internal sealed class ReorderedBehavior<TResponse, TRequest> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest
{
    public ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

internal sealed class OpenPassthroughBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest
{
    public ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}

internal sealed class TransactionalBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : IRequest, ITransactional
{
    public ValueTask<TResponse> HandleAsync(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
}