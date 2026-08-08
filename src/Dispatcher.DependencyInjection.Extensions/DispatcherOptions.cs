using Microsoft.Extensions.DependencyInjection;

namespace Dispatcher.DependencyInjection;

/// <summary>
/// Configures Dispatcher service registration.
/// </summary>
public sealed class DispatcherOptions
{
    /// <summary>
    /// Gets or sets the lifetime used by the current Dispatcher registration operation.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The value is not a defined service lifetime.</exception>
    public ServiceLifetime ServiceLifetime
    {
        get;
        set
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value,
                    "The value is not a defined service lifetime.");
            }

            field = value;
        }
    } = ServiceLifetime.Scoped;
}