using Dispatcher.DependencyInjection;
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

    private sealed class StockAssemblyMarker;
}