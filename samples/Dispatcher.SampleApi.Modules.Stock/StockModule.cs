using Dispatcher.Extensions.DependencyInjection;
using Dispatcher.SampleApi.Modules.Orders;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.SampleApi.Modules.Stock;

public static class StockModule
{
    public static IServiceCollection AddStockModule(this IServiceCollection services)
    {
        services.AddSingleton<StockStore>();
        services.AddScoped<IValidator<SetStockCommand>, SetStockCommandValidator>();
        return services.AddDispatcherHandlers<StockAssemblyMarker>();
    }

    public static IServiceCollection AddStockModuleAot(this IServiceCollection services)
    {
        services.AddSingleton<StockStore>();
        services.AddScoped<IValidator<SetStockCommand>, SetStockCommandValidator>();
        return services
            .AddQueryHandler<GetStockQuery, StockLevel, GetStockQueryHandler>()
            .AddCommandHandler<SetStockCommand, SetStockCommandHandler>()
            .AddNotificationHandler<OrderCreated, ReserveStockWhenOrderCreated>();
    }

    private sealed class StockAssemblyMarker;
}