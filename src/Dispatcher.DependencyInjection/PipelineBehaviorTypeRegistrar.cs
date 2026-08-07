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

        var serviceTypes = behaviorType.GetInterfaces()
            .Where(IsBehaviorInterface)
            .Select(type => behaviorType.IsGenericTypeDefinition ? type.GetGenericTypeDefinition() : type)
            .Distinct()
            .ToArray();

        if (serviceTypes.Length == 0)
        {
            throw new ArgumentException(
                $"Pipeline behavior '{behaviorType.FullName}' does not implement a supported behavior interface.",
                nameof(behaviorType));
        }

        foreach (var serviceType in serviceTypes)
        {
            services.Add(ServiceDescriptor.Describe(serviceType, behaviorType, lifetime));
        }

        return services;
    }

    private static bool IsBehaviorInterface(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>);
}