using Dispatcher.Tests.TestSupport;
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
}
