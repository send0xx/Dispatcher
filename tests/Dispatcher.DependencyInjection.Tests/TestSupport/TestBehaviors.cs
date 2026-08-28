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

internal sealed class FirstSumBehavior(TestState state) : IPipelineBehavior<SumCommand, int>
{
    public async ValueTask<int> HandleAsync(
        SumCommand command,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        state.Record("first-before");
        var result = await next(cancellationToken);
        state.Record("first-after");
        return result;
    }
}

internal sealed class SecondSumBehavior(TestState state) : IPipelineBehavior<SumCommand, int>
{
    public async ValueTask<int> HandleAsync(
        SumCommand command,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        state.Record("second-before");
        var result = await next(cancellationToken);
        state.Record("second-after");
        return result;
    }
}

internal sealed class TransientSumBehavior : IPipelineBehavior<SumCommand, int>
{
    public TransientSumBehavior(TestState state)
    {
        state.BehaviorInstances++;
    }

    public ValueTask<int> HandleAsync(
        SumCommand command,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken) =>
        next(cancellationToken);
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

internal sealed class SecondRecordCommandBehavior(TestState state) : IPipelineBehavior<RecordCommand, Unit>
{
    public async ValueTask<Unit> HandleAsync(
        RecordCommand command,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken)
    {
        state.Record("second-record-before");
        var result = await next(cancellationToken);
        state.Record("second-record-after");
        return result;
    }
}

internal sealed class ShortCircuitRecordCommandBehavior : IPipelineBehavior<RecordCommand, Unit>
{
    public ValueTask<Unit> HandleAsync(
        RecordCommand command,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken) => Unit.ValueTask;
}

internal sealed class TokenReplacingCommandBehavior(CancellationToken replacement)
    : IPipelineBehavior<TokenCommand, CancellationToken>
{
    public ValueTask<CancellationToken> HandleAsync(
        TokenCommand command,
        RequestHandlerDelegate<CancellationToken> next,
        CancellationToken cancellationToken) => next(replacement);
}

internal sealed class TokenReplacingResultlessCommandBehavior(CancellationToken replacement)
    : IPipelineBehavior<TokenRecordingCommand, Unit>
{
    public ValueTask<Unit> HandleAsync(
        TokenRecordingCommand command,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken) => next(replacement);
}

internal sealed class BaseSumBehavior(TestState state) : IPipelineBehavior<BaseSumCommand, int>
{
    public async ValueTask<int> HandleAsync(
        BaseSumCommand command,
        RequestHandlerDelegate<int> next,
        CancellationToken cancellationToken)
    {
        state.Record("base-response-command-before");
        var result = await next(cancellationToken);
        state.Record("base-response-command-after");
        return result;
    }
}

internal sealed class BaseRecordBehavior(TestState state) : IPipelineBehavior<BaseRecordCommand, Unit>
{
    public async ValueTask<Unit> HandleAsync(
        BaseRecordCommand command,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken)
    {
        state.Record("base-command-before");
        var result = await next(cancellationToken);
        state.Record("base-command-after");
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