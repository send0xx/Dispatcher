namespace Dispatcher.DependencyInjection;

/// <summary>
/// Configures tracing and metrics emitted by Dispatcher.
/// </summary>
public sealed class DispatcherTelemetryOptions
{
    private const string DefaultInstrumentationName = "Dispatcher";

    /// <summary>
    /// Gets or sets whether Dispatcher emits operation-duration metrics.
    /// </summary>
    public bool EnableMetrics { get; set; }

    /// <summary>
    /// Gets or sets whether Dispatcher emits tracing activities.
    /// </summary>
    public bool EnableTracing { get; set; }

    /// <summary>
    /// Gets or sets the name of the meter used to emit Dispatcher metrics.
    /// </summary>
    /// <exception cref="ArgumentException">The value is empty or consists only of white-space characters.</exception>
    public string MeterName
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = DefaultInstrumentationName;

    /// <summary>
    /// Gets or sets the name of the activity source used to emit Dispatcher traces.
    /// </summary>
    /// <exception cref="ArgumentException">The value is empty or consists only of white-space characters.</exception>
    public string ActivitySourceName
    {
        get;
        set
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value);
            field = value;
        }
    } = DefaultInstrumentationName;
}
