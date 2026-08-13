namespace Dispatcher.SampleApi.Contracts;

public sealed record Order(Guid Id, string ProductId, int Quantity, DateTimeOffset CreatedAt);

public sealed record CreateOrderCommand(string ProductId, int Quantity) : ICommand<Guid>;

public sealed record GetOrderQuery(Guid Id) : IQuery<Order?>;

public abstract record OrderEvent(Guid OrderId, string ProductId, int Quantity) : INotification;

public sealed record OrderCreated(Guid OrderId, string ProductId, int Quantity)
    : OrderEvent(OrderId, ProductId, Quantity);

public sealed record StockLevel(string ProductId, int Quantity);

public abstract record StockQuery(string ProductId) : IQuery<StockLevel>;

public sealed record GetStockQuery(string ProductId) : StockQuery(ProductId);

public sealed record SetStockCommand(string ProductId, int Quantity) : ICommand;