namespace Dispatcher.Tests.TestSupport;

internal sealed class FirstGreetingBehavior(TestState state) : IPipelineBehavior<GreetingQuery, string>
{
    public async ValueTask<string> HandleAsync(
        GreetingQuery query,
        RequestHandlerDelegate<string> next,
        CancellationToken cancellationToken)
    {
        state.Events.Add("first-before");
        var result = await next(cancellationToken);
        state.Events.Add("first-after");
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
        state.Events.Add("second-before");
        var result = await next(cancellationToken);
        state.Events.Add("second-after");
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

internal sealed class RecordCommandBehavior(TestState state) : IPipelineBehavior<RecordCommand, Unit>
{
    public async ValueTask<Unit> HandleAsync(
        RecordCommand command,
        RequestHandlerDelegate<Unit> next,
        CancellationToken cancellationToken)
    {
        state.Events.Add("record-before");
        var result = await next(cancellationToken);
        state.Events.Add("record-after");
        return result;
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