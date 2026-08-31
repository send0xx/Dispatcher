using System.Diagnostics.CodeAnalysis;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

internal static class PipelineBehaviorTypeRegistrar
{
    [RequiresDynamicCode(CompatibilityMessages.BehaviorDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.BehaviorTrimming)]
    internal static IServiceCollection Register(
        IServiceCollection services,
        Type behaviorType,
        ServiceLifetime lifetime)
    {
        if (!behaviorType.IsClass || behaviorType.IsAbstract)
        {
            throw new ArgumentException(
                $"Pipeline behavior '{behaviorType.FullName}' must be a non-abstract class.",
                nameof(behaviorType));
        }

        var behaviorInterfaces = behaviorType.GetInterfaces()
            .Where(IsBehaviorInterface)
            .ToArray();

        if (behaviorInterfaces.Length == 0)
        {
            throw new ArgumentException(
                $"Pipeline behavior '{behaviorType.FullName}' does not implement a supported behavior interface.",
                nameof(behaviorType));
        }

        Type[] serviceTypes;
        if (behaviorType.IsGenericTypeDefinition)
        {
            var arguments = behaviorType.GetGenericArguments();
            var hasCanonicalShape = arguments.Length == 2 && behaviorInterfaces.Any(@interface =>
            {
                var interfaceArguments = @interface.GetGenericArguments();
                return interfaceArguments[0] == arguments[0] && interfaceArguments[1] == arguments[1];
            });
            if (!hasCanonicalShape)
            {
                throw new ArgumentException(
                    $"Open pipeline behavior '{behaviorType.FullName}' must implement " +
                    "IPipelineBehavior<TRequest, TResponse> using its two generic parameters in order.",
                    nameof(behaviorType));
            }

            serviceTypes = [typeof(IPipelineBehavior<,>)];
        }
        else
        {
            serviceTypes = behaviorInterfaces.Distinct().ToArray();
        }

        foreach (var serviceType in serviceTypes)
        {
            // A behavior registered as an instance carries no implementation type, but its runtime
            // type identifies it just as well. A factory descriptor cannot be matched at all,
            // because Microsoft DI does not expose what a factory will return.
            if (services.Any(descriptor =>
                    descriptor.ServiceType == serviceType &&
                    (descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType()) ==
                    behaviorType))
            {
                continue;
            }

            services.Add(ServiceDescriptor.Describe(serviceType, behaviorType, lifetime));
        }

        return services;
    }

    private static bool IsBehaviorInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>);
}