using System.Collections.Concurrent;
using Dispatcher.SampleApi.Contracts;
using FluentValidation;

namespace Dispatcher.SampleApi.Modules.Orders;

public sealed record ListOrdersQuery : IQuery<IReadOnlyCollection<Order>>;

internal sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(command => command.ProductId).NotEmpty().MaximumLength(50);
        RuleFor(command => command.Quantity).GreaterThan(0);
    }
}

internal sealed class CreateOrderCommandHandler(
    OrderStore store,
    INotificationDispatcher dispatcher) : ICommandHandler<CreateOrderCommand, Guid>
{
    public async ValueTask<Guid> HandleAsync(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var order = new Order(Guid.NewGuid(), command.ProductId, command.Quantity, DateTimeOffset.UtcNow);
        store.Add(order);
        await dispatcher.PublishAsync(
            new OrderCreated(order.Id, order.ProductId, order.Quantity),
            cancellationToken);
        return order.Id;
    }
}

internal sealed class GetOrderQueryHandler(OrderStore store) : IQueryHandler<GetOrderQuery, Order?>
{
    public ValueTask<Order?> HandleAsync(GetOrderQuery query, CancellationToken cancellationToken) =>
        ValueTask.FromResult(store.Find(query.Id));
}

internal sealed class ListOrdersQueryHandler(OrderStore store)
    : IQueryHandler<ListOrdersQuery, IReadOnlyCollection<Order>>
{
    public ValueTask<IReadOnlyCollection<Order>> HandleAsync(
        ListOrdersQuery query,
        CancellationToken cancellationToken) => ValueTask.FromResult(store.List());
}

internal sealed class OrderStore
{
    private readonly ConcurrentDictionary<Guid, Order> _orders = new();

    public void Add(Order order) => _orders[order.Id] = order;

    public Order? Find(Guid id) => _orders.GetValueOrDefault(id);

    public IReadOnlyCollection<Order> List() =>
        _orders.Values.OrderBy(order => order.CreatedAt).ToArray();
}