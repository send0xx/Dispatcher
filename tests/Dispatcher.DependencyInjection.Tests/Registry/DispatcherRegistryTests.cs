using Dispatcher.DependencyInjection.Tests.TestSupport;
using Dispatcher.TestSupport.Contracts;
using Dispatcher.TestSupport.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Registry;

public sealed class DispatcherRegistryTests
{
    [Fact]
    public async Task Registered_registry_includes_routes_discovered_by_handler_scanning()
    {
        var services = new ServiceCollection();
        services.AddDispatcherHandlers<HandlerAssemblyMarker>();
        services.AddDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>().QueryAsync(
            new SharedDerivedQuery("public factory"),
            TestContext.Current.CancellationToken);

        Assert.Equal("Handled public factory", result);
    }

    [Fact]
    public void Duplicate_request_handlers_are_rejected_when_registry_is_built()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueryHandler<GreetingQuery, string>>(_ => null!);
        services.AddSingleton<IQueryHandler<GreetingQuery, string>>(_ => null!);
        services.AddDispatcher();

        using var provider = services.BuildServiceProvider();

        Assert.Throws<DuplicateHandlerException>(provider.GetRequiredService<DispatcherRegistry>);
    }

    [Fact]
    public void Ambiguous_polymorphic_routes_are_rejected_when_registry_is_built()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueryHandler<IFirstQuery, string>>(_ => null!);
        services.AddSingleton<IQueryHandler<ISecondQuery, string>>(_ => null!);
        services.AddDispatcherMessage<AmbiguousQuery>();
        services.AddDispatcher();

        using var provider = services.BuildServiceProvider();
        var exception = Assert.Throws<AmbiguousHandlerException>(
            provider.GetRequiredService<DispatcherRegistry>);

        Assert.Equal(typeof(AmbiguousQuery), exception.MessageType);
        Assert.Equal(
            [typeof(IFirstQuery), typeof(ISecondQuery)],
            exception.CandidateMessageTypes);
    }

    private interface IFirstQuery : IQuery<string>;
    private interface ISecondQuery : IQuery<string>;
    private sealed record AmbiguousQuery : IFirstQuery, ISecondQuery;
}