using Dispatcher.Extensions.DependencyInjection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.SampleApi.Modules.Orders;

public static class OrdersModule
{
    public static IServiceCollection AddOrdersModule(this IServiceCollection services)
    {
        services.AddSingleton<OrderStore>();
        services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
        return services.AddDispatcherHandlers<OrdersAssemblyMarker>();
    }

    public static IServiceCollection AddOrdersModuleAot(this IServiceCollection services)
    {
        services.AddSingleton<OrderStore>();
        services.AddScoped<IValidator<CreateOrderCommand>, CreateOrderCommandValidator>();
        return services
            .AddCommandHandler<CreateOrderCommand, Guid, CreateOrderCommandHandler>()
            .AddQueryHandler<GetOrderQuery, Order?, GetOrderQueryHandler>()
            .AddQueryHandler<ListOrdersQuery, IReadOnlyCollection<Order>, ListOrdersQueryHandler>();
    }

    private sealed class OrdersAssemblyMarker;
}