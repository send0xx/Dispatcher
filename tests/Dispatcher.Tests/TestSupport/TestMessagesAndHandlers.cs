namespace Dispatcher.Tests.TestSupport;

internal sealed record GreetingQuery(string Name) : IQuery<string>;

internal sealed class GreetingQueryHandler(TestState state) : IQueryHandler<GreetingQuery, string>
{
    public ValueTask<string> HandleAsync(GreetingQuery query, CancellationToken cancellationToken)
    {
        state.Events.Add("handler");
        return ValueTask.FromResult($"Hello, {query.Name}");
    }
}

internal sealed record TokenQuery : IQuery<CancellationToken>;

internal sealed class TokenQueryHandler : IQueryHandler<TokenQuery, CancellationToken>
{
    public ValueTask<CancellationToken> HandleAsync(TokenQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(cancellationToken);
}

internal sealed record DelayedQuery : IQuery<string>;

internal sealed class DelayedQueryHandler(TestState state) : IQueryHandler<DelayedQuery, string>
{
    public ValueTask<string> HandleAsync(DelayedQuery query, CancellationToken cancellationToken) =>
        new(state.DelayedQueryCompletion.Task);
}

internal sealed record SumCommand(int Left, int Right) : ICommand<int>;

internal sealed class SumCommandHandler(TestState state) : ICommandHandler<SumCommand, int>
{
    public ValueTask<int> HandleAsync(SumCommand command, CancellationToken cancellationToken)
    {
        state.Events.Add("sum-handler");
        return ValueTask.FromResult(command.Left + command.Right);
    }
}

internal sealed record RecordCommand(string Value) : ICommand;

internal sealed class RecordCommandHandler(TestState state) : ICommandHandler<RecordCommand>
{
    public ValueTask HandleAsync(RecordCommand command, CancellationToken cancellationToken)
    {
        state.Recorded = command.Value;
        return ValueTask.CompletedTask;
    }
}

internal sealed record SomethingHappened : INotification;

internal sealed class ANotificationHandler(TestState state) : INotificationHandler<SomethingHappened>
{
    public ValueTask HandleAsync(SomethingHappened notification, CancellationToken cancellationToken)
    {
        state.Events.Add("notification-a");
        return ValueTask.CompletedTask;
    }
}

internal sealed class BNotificationHandler(TestState state) : INotificationHandler<SomethingHappened>
{
    public ValueTask HandleAsync(SomethingHappened notification, CancellationToken cancellationToken)
    {
        state.Events.Add("notification-b");
        return ValueTask.CompletedTask;
    }
}

internal sealed record UnhandledNotification : INotification;
internal sealed record MissingQuery : IQuery<int>;
internal sealed record FaultingQuery : IQuery<int>;

internal sealed class FaultingQueryHandler : IQueryHandler<FaultingQuery, int>
{
    public ValueTask<int> HandleAsync(FaultingQuery query, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("telemetry failure");
}

internal sealed record CancellingCommand : ICommand;

internal sealed class CancellingCommandHandler : ICommandHandler<CancellingCommand>
{
    public ValueTask HandleAsync(CancellingCommand command, CancellationToken cancellationToken) =>
        ValueTask.FromException(new OperationCanceledException(cancellationToken));
}

internal sealed class AlternativeGreetingHandler;
internal interface ITransactional;
internal sealed record TransactionalQuery : IQuery<string>, ITransactional;