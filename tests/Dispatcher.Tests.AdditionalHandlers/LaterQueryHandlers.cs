using Dispatcher.Tests.Contracts;

namespace Dispatcher.Tests.AdditionalHandlers;

public sealed class AdditionalHandlerAssemblyMarker;

internal sealed class LaterBaseQueryHandler : IQueryHandler<LaterBaseQuery, string>
{
    public ValueTask<string> HandleAsync(
        LaterBaseQuery query,
        CancellationToken cancellationToken) =>
        ValueTask.FromResult("Handled later " + query.Value);
}