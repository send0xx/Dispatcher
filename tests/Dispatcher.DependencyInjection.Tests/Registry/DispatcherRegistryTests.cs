using Dispatcher.DependencyInjection.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Registry;

public sealed class DispatcherRegistryTests
{
    [Fact]
    public void Duplicate_request_handlers_are_rejected_when_registry_is_built()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IQueryHandler<GreetingQuery, string>>(_ => null!);
        services.AddSingleton<IQueryHandler<GreetingQuery, string>>(_ => null!);
        services.AddDispatcher();

        using var provider = services.BuildServiceProvider();

        Assert.Throws<DuplicateHandlerException>(provider.GetRequiredService<IDispatcher>);
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
            provider.GetRequiredService<IDispatcher>);

        Assert.Equal(typeof(AmbiguousQuery), exception.MessageType);
        Assert.Equal(
            [typeof(IFirstQuery), typeof(ISecondQuery)],
            exception.CandidateMessageTypes);
    }

    private interface IFirstQuery : IQuery<string>;

    private interface ISecondQuery : IQuery<string>;

    private sealed record AmbiguousQuery : IFirstQuery, ISecondQuery;
}