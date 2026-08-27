using Dispatcher.DependencyInjection.Tests.TestSupport;
using Dispatcher.TestSupport.Contracts;
using Dispatcher.TestSupport.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Registry;

public sealed class DispatcherRegistryTests
{
    [Fact]
    public void Provider_registry_creation_rejects_a_null_service_provider()
    {
        IServiceProvider serviceProvider = null!;

        Assert.Throws<ArgumentNullException>(serviceProvider.CreateDispatcherRegistry);
    }

    [Fact]
    public async Task Provider_registry_creation_includes_routes_discovered_by_handler_scanning()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<HandlerAssemblyMarker>();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        var registry = scope.ServiceProvider.CreateDispatcherRegistry();
        var dispatcher = new Dispatcher(scope.ServiceProvider, registry);
        var result = await dispatcher.QueryAsync(
            new SharedDerivedQuery("public factory"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Handled public factory", result);
    }

    [Fact]
    public void Duplicate_request_handlers_are_rejected_when_registry_is_built()
    {
        var registrations = new[]
        {
            new QueryHandlerRegistration(typeof(GreetingQuery), typeof(string), typeof(GreetingQueryHandler)),
            new QueryHandlerRegistration(typeof(GreetingQuery), typeof(string), typeof(AlternativeGreetingHandler))
        };

        var services = new ServiceCollection();
        foreach (var registration in registrations)
        {
            services.AddSingleton<HandlerRegistration>(registration);
        }

        using var provider = services.BuildServiceProvider();

        Assert.Throws<DuplicateHandlerException>(provider.CreateDispatcherRegistry);
    }

    [Fact]
    public void Ambiguous_polymorphic_routes_are_rejected_when_registry_is_built()
    {
        MessageRegistration[] routeTargets =
        [
            new(typeof(AmbiguousQuery))
        ];
        HandlerRegistration[] handlers =
        [
            new QueryHandlerRegistration(typeof(IFirstQuery), typeof(string), typeof(FirstHandler)),
            new QueryHandlerRegistration(typeof(ISecondQuery), typeof(string), typeof(SecondHandler))
        ];

        var services = new ServiceCollection();
        foreach (var handler in handlers)
        {
            services.AddSingleton<HandlerRegistration>(handler);
        }

        foreach (var routeTarget in routeTargets)
        {
            services.AddSingleton(routeTarget);
        }

        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<AmbiguousHandlerException>(provider.CreateDispatcherRegistry);

        Assert.Equal(typeof(AmbiguousQuery), exception.MessageType);
        Assert.Equal(
            [typeof(IFirstQuery), typeof(ISecondQuery)],
            exception.CandidateMessageTypes);
    }

    // Registry creation routes by registration metadata alone, so these stand in for handler types
    // without implementing a handler interface.
    private sealed class AlternativeGreetingHandler;
    private interface IFirstQuery : IQuery<string>;
    private interface ISecondQuery : IQuery<string>;
    private sealed record AmbiguousQuery : IFirstQuery, ISecondQuery;
    private sealed class FirstHandler;
    private sealed class SecondHandler;
}