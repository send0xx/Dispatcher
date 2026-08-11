using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher;

/// <summary>
/// Configures Dispatcher service registration.
/// </summary>
public sealed class DispatcherOptions
{
    /// <summary>
    /// Gets or sets the lifetime used by the current Dispatcher registration operation.
    /// </summary>
    public ServiceLifetime ServiceLifetime { get; set; } = ServiceLifetime.Scoped;

    /// <summary>
    /// Gets the telemetry configuration used when registering Dispatcher infrastructure.
    /// </summary>
    public DispatcherTelemetryOptions Telemetry => field ??= new DispatcherTelemetryOptions();
}
