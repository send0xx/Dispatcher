using Dispatcher;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

[assembly: GenerateDispatcherHandlers("AddCounterHandlers")]
[assembly: GenerateDispatcher("AddDispatcher")]

namespace Dispatcher.NativeAotSample.Module;

public static class CounterModule
{
    public static IServiceCollection AddCounterModule(this IServiceCollection services)
    {
        services.AddSingleton<CounterState>();
        services.AddScoped<IValidator<IncrementCounterCommand>, IncrementCounterCommandValidator>();
        return services.AddCounterHandlers().AddDispatcher();
    }
}