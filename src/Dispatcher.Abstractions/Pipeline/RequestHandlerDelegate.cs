namespace Dispatcher;

/// <summary>
/// Invokes the next component in a request pipeline.
/// </summary>
/// <typeparam name="TResponse">The response type.</typeparam>
/// <param name="cancellationToken">A token that can cancel the operation.</param>
/// <returns>The pipeline response.</returns>
public delegate ValueTask<TResponse> RequestHandlerDelegate<TResponse>(
    CancellationToken cancellationToken = default);