using Dispatcher.Tests.Contracts;

namespace Dispatcher.Tests.Handlers;

public sealed class HandlerAssemblyMarker;

internal sealed class SharedBaseQueryHandler : IQueryHandler<SharedBaseQuery, string>
{
    public ValueTask<string> HandleAsync(
        SharedBaseQuery query,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult("Handled " + query.Value);
}