using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher;

/// <summary>
/// Represents configuration for a Dispatcher service registration operation.
/// </summary>
public sealed class DispatcherOptions
{
    /// <summary>
    /// Gets or sets the lifetime used by the current Dispatcher registration operation.
    /// </summary>
    /// <value>The service lifetime. The default is <see cref="ServiceLifetime.Scoped"/>.</value>
    public ServiceLifetime ServiceLifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Gets the telemetry configuration used when registering Dispatcher infrastructure.
    /// </summary>
    /// <value>The telemetry configuration for the current registration operation.</value>
    public DispatcherTelemetryOptions Telemetry => field ??= new DispatcherTelemetryOptions();
}