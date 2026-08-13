namespace Dispatcher;

/// <summary>
/// Represents a delegate that invokes the next component in a request pipeline.
/// </summary>
/// <typeparam name="TResponse">The type of response produced by the pipeline.</typeparam>
/// <param name="cancellationToken">The cancellation token to pass to the next pipeline component.</param>
/// <returns>A value task whose result contains the pipeline response.</returns>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>(
    CancellationToken cancellationToken = default);