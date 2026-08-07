using Dispatcher.DependencyInjection;
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

    private sealed class OrdersAssemblyMarker;
}