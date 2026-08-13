using System.Collections.Concurrent;
using Dispatcher.SampleApi.Contracts;
using FluentValidation;

namespace Dispatcher.SampleApi.Modules.Stock;

internal sealed class SetStockCommandValidator : AbstractValidator<SetStockCommand>
{
    public SetStockCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Quantity).GreaterThanOrEqualTo(0);
    }
}

internal sealed class GetStockQueryHandler(StockStore store) : IQueryHandler<StockQuery, StockLevel>
{
    public ValueTask<StockLevel> HandleAsync(StockQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(new StockLevel(query.ProductId, store.Get(query.ProductId)));
}

internal sealed class SetStockCommandHandler(StockStore store) : ICommandHandler<SetStockCommand>
{
    public ValueTask HandleAsync(SetStockCommand command, CancellationToken cancellationToken)
    {
        store.Set(command.ProductId, command.Quantity);
        return ValueTask.CompletedTask;
    }
}

internal sealed class ReserveStockWhenOrderCreated(StockStore store)
    : INotificationHandler<OrderEvent>
{
    public ValueTask HandleAsync(OrderEvent notification, CancellationToken cancellationToken)
    {
        store.Remove(notification.ProductId, notification.Quantity);
        return ValueTask.CompletedTask;
    }
}

internal sealed class StockStore
{
    private readonly ConcurrentDictionary<string, int> _stock = new(StringComparer.OrdinalIgnoreCase);

    public int Get(string productId) => _stock.GetValueOrDefault(productId);

    public void Set(string productId, int quantity) => _stock[productId] = quantity;

    public void Remove(string productId, int quantity) =>
        _stock.AddOrUpdate(productId, -quantity, (_, current) => current - quantity);
}