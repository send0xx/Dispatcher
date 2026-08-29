namespace Dispatcher.DependencyInjection.Tests.TestSupport;

internal abstract record OrderEvent : INotification;

internal sealed record OrderCreated : OrderEvent;

internal sealed record OrderShipped : OrderEvent;

internal sealed class OrderCreatedEventHandler(TestState state) : INotificationHandler<OrderCreated>
{
    public ValueTask HandleAsync(OrderCreated notification, CancellationToken cancellationToken)
    {
        state.Record("order-created");
        return ValueTask.CompletedTask;
    }
}

internal sealed class OrderEventHandler(TestState state) : INotificationHandler<OrderEvent>
{
    public ValueTask HandleAsync(OrderEvent notification, CancellationToken cancellationToken)
    {
        state.Record("order-event");
        return ValueTask.CompletedTask;
    }
}

internal sealed class OrderEvents<TOrderEvent>(TestState state) : INotificationHandler<TOrderEvent>
    where TOrderEvent : OrderEvent
{
    public ValueTask HandleAsync(TOrderEvent notification, CancellationToken cancellationToken)
    {
        state.Record("open-" + typeof(TOrderEvent).Name);
        return ValueTask.CompletedTask;
    }
}