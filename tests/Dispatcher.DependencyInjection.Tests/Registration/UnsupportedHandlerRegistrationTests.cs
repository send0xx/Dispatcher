using System.Reflection;
using System.Runtime.Loader;
using Dispatcher.TestSupport.Contracts;
using Dispatcher.TestSupport.Handlers;
using Dispatcher.TestSupport.UnsupportedHandlers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Registration;

public sealed class UnsupportedHandlerRegistrationTests
{
    [Fact]
    public void Scanning_reports_every_unsupported_handler_in_one_exception()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<UnsupportedHandlerException>(() =>
            services.AddDispatcherHandlers(typeof(UnsupportedHandlerAssemblyMarker).Assembly));

        Assert.Equal(
            [
                typeof(ClosedHandlerWithoutPublicConstructor),
                typeof(GenericHandlerWithClosedNotification<>),
                typeof(OpenGenericQueryHandler<>),
                typeof(OpenNotificationHandlerWithoutPublicConstructor<>)
            ],
            exception.Handlers.Keys.OrderBy(handler => handler.FullName, StringComparer.Ordinal));
    }

    [Fact]
    public void Unsupported_handlers_are_reported_with_the_reason_they_cannot_be_registered()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<UnsupportedHandlerException>(() =>
            services.AddDispatcherHandlers(typeof(UnsupportedHandlerAssemblyMarker).Assembly));

        Assert.Contains(
            "public constructor",
            exception.Handlers[typeof(ClosedHandlerWithoutPublicConstructor)]);
        Assert.Contains(
            "public constructor",
            exception.Handlers[typeof(OpenNotificationHandlerWithoutPublicConstructor<>)]);
        Assert.Contains(
            "not a supported open generic handler",
            exception.Handlers[typeof(GenericHandlerWithClosedNotification<>)]);
        Assert.Contains(
            "not a supported open generic handler",
            exception.Handlers[typeof(OpenGenericQueryHandler<>)]);
    }

    [Fact]
    public void A_failed_scan_leaves_the_service_collection_untouched()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new object());
        var before = services.ToArray();

        Assert.Throws<UnsupportedHandlerException>(() =>
            services.AddDispatcherHandlers(typeof(UnsupportedHandlerAssemblyMarker).Assembly));

        Assert.Equal(before, services);
    }

    [Fact]
    public void A_null_assembly_leaves_a_multi_assembly_scan_untouched()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new object());
        var before = services.ToArray();

        Assert.Throws<ArgumentNullException>(() =>
        {
            // The array and the argument name pick the assemblies overload; passing the elements
            // directly is ambiguous with the (assembly, configure) overload.
#pragma warning disable S3878
            services.AddDispatcherHandlers(
                assemblies: [typeof(HandlerAssemblyMarker).Assembly, null!]);
#pragma warning restore S3878
        });

        Assert.Equal(before, services);
    }

    [Fact]
    public void Scanning_fails_when_an_assembly_declares_types_that_cannot_be_loaded()
    {
        // The handler assembly is loaded into a context that cannot resolve the contracts assembly
        // its handlers derive from, so those handler types fail to load. Continuing with the types
        // that did load would drop them silently.
        var context = new BlockedDependencyLoadContext("Dispatcher.TestSupport.Contracts");
        try
        {
            var assembly = context.LoadFromAssemblyPath(typeof(HandlerAssemblyMarker).Assembly.Location);
            var services = new ServiceCollection();

            var exception = Assert.Throws<AssemblyScanException>(() =>
                services.AddDispatcherHandlers(assembly));

            Assert.Same(assembly, exception.Assembly);
            Assert.NotEmpty(exception.LoaderExceptions);
            Assert.Same(exception.LoaderExceptions, exception.LoaderExceptions);
            Assert.Empty(services);
        }
        finally
        {
            context.Unload();
        }
    }

    [Fact]
    public void Scanning_is_atomic_when_a_route_target_assembly_cannot_load_all_types()
    {
        var context = new RouteTargetLoadContext(
            typeof(SharedBaseQuery).Assembly.Location,
            "Dispatcher.TestSupport.TypeLoadFailureDependency");
        try
        {
            var assembly = context.LoadFromAssemblyPath(typeof(HandlerAssemblyMarker).Assembly.Location);
            var services = new ServiceCollection();
            services.AddSingleton(new object());
            var before = services.ToArray();

            var exception = Assert.Throws<AssemblyScanException>(() =>
                services.AddDispatcherHandlers(assembly));

            Assert.Equal(typeof(SharedBaseQuery).Assembly.GetName().Name, exception.Assembly.GetName().Name);
            Assert.Equal(before, services);
        }
        finally
        {
            context.Unload();
        }
    }

    /// <summary>
    /// Loads an assembly while refusing to resolve one of its dependencies, so that the types
    /// needing that dependency fail to load.
    /// </summary>
    private sealed class BlockedDependencyLoadContext(string blockedAssemblyName)
        : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName) =>
            assemblyName.Name == blockedAssemblyName
                ? throw new FileNotFoundException($"'{assemblyName.Name}' is blocked by this test.")
                : null;
    }

    // ReSharper disable once ParameterOnlyUsedForPreconditionCheck.Local
    private sealed class RouteTargetLoadContext(string contractsAssemblyPath, string blockedAssemblyName)
        : AssemblyLoadContext(isCollectible: true)
    {
        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == blockedAssemblyName)
            {
                throw new FileNotFoundException($"'{assemblyName.Name}' is blocked by this test.");
            }

            return assemblyName.Name == "Dispatcher.TestSupport.Contracts"
                ? LoadFromAssemblyPath(contractsAssemblyPath)
                : null;
        }
    }
}