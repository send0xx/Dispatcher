using System.Collections.Frozen;
using System.Reflection;
using Dispatcher.Tests.TestSupport;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Dispatcher.Tests.Registry;

public sealed class DispatcherRegistryTests
{
    [Fact]
    public void Duplicate_request_handlers_are_rejected_when_registry_is_built()
    {
        var registrations = new[]
        {
            new QueryHandlerRegistration(typeof(GreetingQuery), typeof(string), typeof(GreetingQueryHandler)),
            new QueryHandlerRegistration(typeof(GreetingQuery), typeof(string), typeof(AlternativeGreetingHandler))
        };

        Assert.Throws<DuplicateHandlerException>(() => DispatcherRegistry.Create(registrations));
    }

    [Fact]
    public async Task Registry_uses_frozen_dictionaries()
    {
        await using var provider = TestServices.CreateProvider();
        var registry = provider.GetRequiredService<DispatcherRegistry>();
        var properties = typeof(DispatcherRegistry).GetProperties(BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.All(properties, property =>
            Assert.Equal(typeof(FrozenDictionary<,>), property.PropertyType.GetGenericTypeDefinition()));
    }
}