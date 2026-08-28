using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

internal sealed class DispatcherRegistrationState
{
    internal HashSet<Assembly> HandlerAssemblies { get; } = [];

    internal HashSet<Assembly> MessageAssemblies { get; } = [];

    internal HashSet<Type> MessageTypes { get; } = [];

    internal static DispatcherRegistrationState? Find(IServiceCollection services)
    {
        foreach (var descriptor in services)
        {
            if (descriptor.ServiceType == typeof(DispatcherRegistrationState) &&
                descriptor.ImplementationInstance is DispatcherRegistrationState state)
            {
                return state;
            }
        }

        return null;
    }

    internal static DispatcherRegistrationState GetOrCreate(IServiceCollection services)
    {
        if (Find(services) is { } state)
        {
            return state;
        }

        return Create(services);
    }

    internal static DispatcherRegistrationState Create(IServiceCollection services)
    {
        var created = new DispatcherRegistrationState();
        services.AddSingleton(created);
        return created;
    }
}