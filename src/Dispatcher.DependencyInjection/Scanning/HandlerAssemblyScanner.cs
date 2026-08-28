using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Registers the handlers declared by one or more assemblies, together with the message metadata
/// their routes need. Scanning an assembly twice is a no-op, and registering a handler that another
/// path already registered is too.
/// </summary>
internal static class HandlerAssemblyScanner
{
    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    internal static IServiceCollection Register(
        IServiceCollection services,
        IEnumerable<Assembly> assemblies,
        ServiceLifetime lifetime)
    {
        var plan = AssemblyScanPlan.Create(AssemblyScanState.Find(services), assemblies);
        if (plan.IsEmpty)
        {
            return services;
        }

        var existing = ExistingRegistrations.Read(services, plan.Candidates);
        RegisterHandlers(services, plan.Candidates, lifetime, existing);
        plan.Record(AssemblyScanState.GetOrCreate(services), existing.Handled);

        return services;
    }

    [RequiresDynamicCode(CompatibilityMessages.HandlerDynamicCode)]
    [RequiresUnreferencedCode(CompatibilityMessages.HandlerTrimming)]
    private static void RegisterHandlers(
        IServiceCollection services,
        IEnumerable<HandlerCandidate> candidates,
        ServiceLifetime lifetime,
        ExistingRegistrations existing)
    {
        foreach (var candidate in candidates)
        {
            if (existing.TryClaimServiceDescriptor(candidate))
            {
                services.Add(ServiceDescriptor.Describe(
                    candidate.ServiceType,
                    candidate.ImplementationType,
                    lifetime));
            }

            existing.Handled.Add(candidate.Descriptor);
        }
    }
}