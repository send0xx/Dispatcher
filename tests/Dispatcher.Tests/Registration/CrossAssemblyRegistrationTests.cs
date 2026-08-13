using Dispatcher.DependencyInjection;
using Dispatcher.Tests.Contracts;
using Dispatcher.Tests.Handlers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.Tests.Registration;

public sealed class CrossAssemblyRegistrationTests
{
    [Fact]
    public async Task Handler_scan_discovers_derived_messages_from_the_shared_contracts_assembly()
    {
        var services = new ServiceCollection();
        services
            .AddDispatcherHandlers<HandlerAssemblyMarker>()
            .AddDispatcher();
        await using var provider = services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true,
            ValidateScopes = true
        });
        await using var scope = provider.CreateAsyncScope();

        var result = await scope.ServiceProvider.GetRequiredService<IQueryDispatcher>()
            .QueryAsync(new SharedDerivedQuery("across assemblies"));

        Assert.Equal("Handled across assemblies", result);
    }
}