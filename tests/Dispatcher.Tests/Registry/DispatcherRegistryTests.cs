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

        Assert.Throws<DuplicateHandlerException>(() => DispatcherRegistry.Create(registrations, telemetry: null));
    }

    [Fact]
    public void Ambiguous_polymorphic_routes_are_rejected_when_registry_is_built()
    {
        MessageRegistration[] registrations =
        [
            new(typeof(AmbiguousQuery)),
            new QueryHandlerRegistration(typeof(IFirstQuery), typeof(string), typeof(FirstHandler)),
            new QueryHandlerRegistration(typeof(ISecondQuery), typeof(string), typeof(SecondHandler))
        ];

        var exception = Assert.Throws<AmbiguousHandlerException>(() =>
            DispatcherRegistry.Create(registrations, telemetry: null));

        Assert.Equal(typeof(AmbiguousQuery), exception.MessageType);
        Assert.Equal(
            [typeof(IFirstQuery), typeof(ISecondQuery)],
            exception.CandidateMessageTypes);
    }

    private interface IFirstQuery : IQuery<string>;
    private interface ISecondQuery : IQuery<string>;
    private sealed record AmbiguousQuery : IFirstQuery, ISecondQuery;
    private sealed class FirstHandler;
    private sealed class SecondHandler;
}