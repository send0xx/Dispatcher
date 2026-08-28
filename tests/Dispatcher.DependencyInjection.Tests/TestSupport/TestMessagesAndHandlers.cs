namespace Dispatcher.DependencyInjection.Tests.TestSupport;

internal sealed record GreetingQuery(string Name) : IQuery<string>;

internal sealed class GreetingQueryHandler(TestState state) : IQueryHandler<GreetingQuery, string>
{
    public ValueTask<string> HandleAsync(GreetingQuery query, CancellationToken cancellationToken)
    {
        state.Record("handler");
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
        state.Record("sum-handler");
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

internal sealed record TokenCommand : ICommand<CancellationToken>;

internal sealed class TokenCommandHandler : ICommandHandler<TokenCommand, CancellationToken>
{
    public ValueTask<CancellationToken> HandleAsync(
        TokenCommand command,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(cancellationToken);
}

internal sealed record TokenRecordingCommand : ICommand;

internal sealed class TokenRecordingCommandHandler(TestState state) : ICommandHandler<TokenRecordingCommand>
{
    public ValueTask HandleAsync(TokenRecordingCommand command, CancellationToken cancellationToken)
    {
        state.ReceivedToken = cancellationToken;
        return ValueTask.CompletedTask;
    }
}

internal sealed record SomethingHappened : INotification;

internal sealed class ANotificationHandler(TestState state) : INotificationHandler<SomethingHappened>
{
    public ValueTask HandleAsync(SomethingHappened notification, CancellationToken cancellationToken)
    {
        state.Record("notification-a");
        return ValueTask.CompletedTask;
    }
}

internal sealed class BNotificationHandler(TestState state) : INotificationHandler<SomethingHappened>
{
    public ValueTask HandleAsync(SomethingHappened notification, CancellationToken cancellationToken)
    {
        state.Record("notification-b");
        return ValueTask.CompletedTask;
    }
}

internal sealed record UnhandledNotification : INotification;

internal sealed record MissingCommand : ICommand;

internal sealed record MissingResponseCommand : ICommand<int>;

// Messages that satisfy two dispatch shapes but are handled as one of them, so dispatching them
// through the other shape must fail instead of resolving the wrong wrapper.
internal sealed record CommandShapedQuery : IQuery<string>, ICommand<string>;

internal sealed class CommandShapedQueryHandler : ICommandHandler<CommandShapedQuery, string>
{
    public ValueTask<string> HandleAsync(CommandShapedQuery command, CancellationToken cancellationToken) =>
        ValueTask.FromResult("command");
}

internal sealed record QueryShapedCommand : ICommand<int>, IQuery<int>;

internal sealed class QueryShapedCommandHandler : IQueryHandler<QueryShapedCommand, int>
{
    public ValueTask<int> HandleAsync(QueryShapedCommand query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(1);
}

internal sealed record QueryShapedResultlessCommand : ICommand, IQuery<string>;

internal sealed class QueryShapedResultlessCommandHandler
    : IQueryHandler<QueryShapedResultlessCommand, string>
{
    public ValueTask<string> HandleAsync(
        QueryShapedResultlessCommand query,
        CancellationToken cancellationToken) => ValueTask.FromResult("query");
}

internal sealed record FaultingNotification : INotification;

internal sealed class FaultingNotificationHandler : INotificationHandler<FaultingNotification>
{
    public ValueTask HandleAsync(FaultingNotification notification, CancellationToken cancellationToken) =>
        throw new InvalidOperationException("notification failure");
}

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

internal interface ITransactional;

internal sealed record TransactionalQuery : IQuery<string>, ITransactional;

internal abstract record BaseGreetingQuery(string Name) : IQuery<string>;

internal sealed record DerivedGreetingQuery(string Name) : BaseGreetingQuery(Name);

internal sealed record SpecificGreetingQuery(string Name) : BaseGreetingQuery(Name);

internal sealed class BaseGreetingQueryHandler(TestState state)
    : IQueryHandler<BaseGreetingQuery, string>
{
    public ValueTask<string> HandleAsync(
        BaseGreetingQuery query,
        CancellationToken cancellationToken)
    {
        state.Record("base-query");
        return ValueTask.FromResult($"Base hello, {query.Name}");
    }
}

internal sealed class SpecificGreetingQueryHandler(TestState state)
    : IQueryHandler<SpecificGreetingQuery, string>
{
    public ValueTask<string> HandleAsync(
        SpecificGreetingQuery query,
        CancellationToken cancellationToken)
    {
        state.Record("specific-query");
        return ValueTask.FromResult($"Specific hello, {query.Name}");
    }
}

internal interface IVehicleQuery : IQuery<string>;

internal interface ICarQuery : IVehicleQuery;

internal sealed record CarQuery : ICarQuery;

internal sealed class VehicleQueryHandler(TestState state) : IQueryHandler<IVehicleQuery, string>
{
    public ValueTask<string> HandleAsync(IVehicleQuery query, CancellationToken cancellationToken)
    {
        state.Record("vehicle-query");
        return ValueTask.FromResult("vehicle");
    }
}

internal sealed class CarQueryHandler(TestState state) : IQueryHandler<ICarQuery, string>
{
    public ValueTask<string> HandleAsync(ICarQuery query, CancellationToken cancellationToken)
    {
        state.Record("car-query");
        return ValueTask.FromResult("car");
    }
}

internal abstract record BaseSumCommand(int Left, int Right) : ICommand<int>;

internal sealed record DerivedSumCommand(int Left, int Right) : BaseSumCommand(Left, Right);

internal sealed class BaseSumCommandHandler(TestState state)
    : ICommandHandler<BaseSumCommand, int>
{
    public ValueTask<int> HandleAsync(
        BaseSumCommand command,
        CancellationToken cancellationToken)
    {
        state.Record("base-response-command");
        return ValueTask.FromResult(command.Left + command.Right);
    }
}

internal abstract record BaseRecordCommand(string Value) : ICommand;

internal sealed record DerivedRecordCommand(string Value) : BaseRecordCommand(Value);

internal sealed class BaseRecordCommandHandler(TestState state)
    : ICommandHandler<BaseRecordCommand>
{
    public ValueTask HandleAsync(
        BaseRecordCommand command,
        CancellationToken cancellationToken)
    {
        state.Record("base-command");
        state.Recorded = command.Value;
        return ValueTask.CompletedTask;
    }
}

internal abstract record DomainEvent : INotification;

internal sealed record UserUpdatedEvent : DomainEvent;

internal sealed record UserCreatedEvent : DomainEvent;

internal sealed class FirstDomainEventHandler(TestState state) : INotificationHandler<DomainEvent>
{
    public ValueTask HandleAsync(DomainEvent notification, CancellationToken cancellationToken)
    {
        state.Record("domain-a");
        return ValueTask.CompletedTask;
    }
}

internal sealed class SecondDomainEventHandler(TestState state) : INotificationHandler<DomainEvent>
{
    public ValueTask HandleAsync(DomainEvent notification, CancellationToken cancellationToken)
    {
        state.Record("domain-b");
        return ValueTask.CompletedTask;
    }
}

internal sealed class UserCreatedEventHandler(TestState state) : INotificationHandler<UserCreatedEvent>
{
    public ValueTask HandleAsync(UserCreatedEvent notification, CancellationToken cancellationToken)
    {
        state.Record("user-created");
        return ValueTask.CompletedTask;
    }
}