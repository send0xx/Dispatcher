namespace Dispatcher;

/// <summary>
/// Represents configuration for tracing and metrics emitted by Dispatcher.
/// </summary>
public sealed class DispatcherTelemetryOptions
{
    private const string DefaultInstrumentationName = "Dispatcher";

    /// <summary>
    /// Gets or sets whether Dispatcher emits operation-duration metrics.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to emit metrics; otherwise, <see langword="false"/>.
    /// The default is <see langword="false"/>.
    /// </value>
    public bool EnableMetrics { get; set; }

    /// <summary>
    /// Gets or sets whether Dispatcher emits tracing activities.
    /// </summary>
    /// <value>
    /// <see langword="true"/> to emit tracing activities; otherwise, <see langword="false"/>.
    /// The default is <see langword="false"/>.
    /// </value>
    public bool EnableTracing { get; set; }

    /// <summary>
    /// Gets or sets the name of the meter used to emit Dispatcher metrics.
    /// </summary>
    /// <value>The meter name. The default is <c>Dispatcher</c>.</value>
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
    /// <value>The activity source name. The default is <c>Dispatcher</c>.</value>
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