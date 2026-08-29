namespace Dispatcher.DependencyInjection.Tests.TestSupport;

internal abstract record InventoryEvent : INotification;

internal sealed record StockAdjusted : InventoryEvent;

internal class InventoryEventHandler<TInventoryEvent>(TestState state)
    : INotificationHandler<TInventoryEvent>
    where TInventoryEvent : InventoryEvent
{
    protected TestState State { get; } = state;

    public virtual ValueTask HandleAsync(TInventoryEvent notification, CancellationToken cancellationToken)
    {
        State.Record("inventory-base-" + typeof(TInventoryEvent).Name);
        return ValueTask.CompletedTask;
    }
}

internal sealed class StockAdjustedHandler(TestState state) : InventoryEventHandler<StockAdjusted>(state)
{
    public override ValueTask HandleAsync(StockAdjusted notification, CancellationToken cancellationToken)
    {
        State.Record("stock-adjusted-override");
        return ValueTask.CompletedTask;
    }
}