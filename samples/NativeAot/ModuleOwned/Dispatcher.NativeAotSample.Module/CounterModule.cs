using Dispatcher;
using Dispatcher.Extensions.DependencyInjection;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

[assembly: GenerateDispatcherHandlers("AddGeneratedCounterHandlers")]

namespace Dispatcher.NativeAotSample.Module;

public static class CounterModule
{
    public static IServiceCollection AddCounterModule(this IServiceCollection services)
    {
        services.AddSingleton<CounterState>();
        services.AddScoped<IValidator<IncrementCounterCommand>, IncrementCounterCommandValidator>();
        return services.AddGeneratedCounterHandlers();
    }
}