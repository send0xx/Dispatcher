using System.Collections.Concurrent;
using Dispatcher.SampleApi.Modules.Orders;
using FluentValidation;

namespace Dispatcher.SampleApi.Modules.Stock;

public sealed record StockLevel(string ProductId, int Quantity);

public sealed record GetStockQuery(string ProductId) : IQuery<StockLevel>;

public sealed record SetStockCommand(string ProductId, int Quantity) : ICommand;

internal sealed class SetStockCommandValidator : AbstractValidator<SetStockCommand>
{
    public SetStockCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Quantity).GreaterThanOrEqualTo(0);
    }
}

internal sealed class GetStockQueryHandler(StockStore store) : IQueryHandler<GetStockQuery, StockLevel>
{
    public ValueTask<StockLevel> HandleAsync(GetStockQuery query, CancellationToken cancellationToken) =>
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
    : INotificationHandler<OrderCreated>
{
    public ValueTask HandleAsync(OrderCreated notification, CancellationToken cancellationToken)
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