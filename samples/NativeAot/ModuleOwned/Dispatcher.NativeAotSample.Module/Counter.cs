using FluentValidation;

namespace Dispatcher.NativeAotSample.Module;

public sealed record GetCounterQuery : IQuery<CounterSnapshot>;
public sealed record IncrementCounterCommand(int Amount) : ICommand<int>;
public sealed record ResetCounterCommand : ICommand;
public sealed record CounterChanged(int Value) : INotification;
public sealed record CounterSnapshot(
    int Value,
    int LastPublishedValue,
    int ChangeNotificationsObserved);

internal sealed class IncrementCounterCommandValidator : AbstractValidator<IncrementCounterCommand>
{
    public IncrementCounterCommandValidator()
    {
        RuleFor(command => command.Amount).InclusiveBetween(1, 10);
    }
}

internal sealed class GetCounterQueryHandler(CounterState state)
    : IQueryHandler<GetCounterQuery, CounterSnapshot>
{
    public ValueTask<CounterSnapshot> HandleAsync(
        GetCounterQuery query,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult(state.Snapshot());
}

internal sealed class IncrementCounterCommandHandler(
    CounterState state,
    INotificationPublisher publisher) : ICommandHandler<IncrementCounterCommand, int>
{
    public async ValueTask<int> HandleAsync(
        IncrementCounterCommand command,
        CancellationToken cancellationToken)
    {
        var value = state.Increment(command.Amount);
        await publisher.PublishAsync(new CounterChanged(value), cancellationToken);
        return value;
    }
}

internal sealed class ResetCounterCommandHandler(
    CounterState state,
    INotificationPublisher publisher) : ICommandHandler<ResetCounterCommand>
{
    public async ValueTask HandleAsync(
        ResetCounterCommand command,
        CancellationToken cancellationToken)
    {
        state.Reset();
        await publisher.PublishAsync(new CounterChanged(0), cancellationToken);
    }
}

internal sealed class RecordPublishedCounterHandler(CounterState state)
    : INotificationHandler<CounterChanged>
{
    public ValueTask HandleAsync(
        CounterChanged notification,
        CancellationToken cancellationToken)
    {
        state.RecordPublishedValue(notification.Value);
        return ValueTask.CompletedTask;
    }
}

internal sealed class CountCounterChangesHandler(CounterState state)
    : INotificationHandler<CounterChanged>
{
    public ValueTask HandleAsync(
        CounterChanged notification,
        CancellationToken cancellationToken)
    {
        state.RecordChangeNotification();
        return ValueTask.CompletedTask;
    }
}

internal sealed class CounterState
{
    private readonly Lock _lock = new();
    private int _value;
    private int _lastPublishedValue;
    private int _changeNotificationsObserved;

    public int Increment(int amount)
    {
        lock (_lock)
        {
            _value += amount;
            return _value;
        }
    }

    public void Reset()
    {
        lock (_lock)
        {
            _value = 0;
        }
    }

    public void RecordPublishedValue(int value)
    {
        lock (_lock)
        {
            _lastPublishedValue = value;
        }
    }

    public void RecordChangeNotification()
    {
        lock (_lock)
        {
            _changeNotificationsObserved++;
        }
    }

    public CounterSnapshot Snapshot()
    {
        lock (_lock)
        {
            return new CounterSnapshot(
                _value,
                _lastPublishedValue,
                _changeNotificationsObserved);
        }
    }
}