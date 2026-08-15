using System.Reflection;

namespace Dispatcher;

/// <summary>
/// The exception thrown when assembly scanning cannot read every type of an assembly, and therefore
/// cannot tell whether the types it could not read declare handlers.
/// </summary>
public sealed class AssemblyScanException : InvalidOperationException
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AssemblyScanException"/> class.
    /// </summary>
    /// <param name="assembly">The assembly whose types could not all be read.</param>
    /// <param name="innerException">The reflection failure that reports why, per type.</param>
    /// <exception cref="ArgumentNullException">
    /// <paramref name="assembly"/> or <paramref name="innerException"/> is <see langword="null"/>.
    /// </exception>
    public AssemblyScanException(Assembly assembly, ReflectionTypeLoadException innerException)
        : base(CreateMessage(assembly, innerException), innerException)
    {
        Assembly = assembly;
    }

    /// <summary>
    /// Gets the assembly whose types could not all be read.
    /// </summary>
    public Assembly Assembly { get; }

    /// <summary>
    /// Gets the reasons the types could not be read, one per unreadable type.
    /// </summary>
    /// <value>
    /// The loader exceptions of the underlying <see cref="ReflectionTypeLoadException"/>, with the
    /// entries it leaves <see langword="null"/> removed.
    /// </value>
    public IReadOnlyList<Exception> LoaderExceptions =>
        ((ReflectionTypeLoadException)InnerException!).LoaderExceptions.OfType<Exception>().ToArray();

    private static string CreateMessage(Assembly assembly, ReflectionTypeLoadException innerException)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        ArgumentNullException.ThrowIfNull(innerException);

        var unreadableCount = innerException.Types.Count(static type => type is null);
        var reasons = innerException.LoaderExceptions
            .OfType<Exception>()
            .Select(static loaderException => loaderException.Message)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static message => message, StringComparer.Ordinal)
            .Select(static message => $"{Environment.NewLine}  - {message}");

        return $"Assembly '{assembly.FullName}' has {unreadableCount} type(s) that cannot be loaded, " +
            "so scanning cannot tell whether they declare handlers. Resolve the assembly's missing " +
            "dependencies, or scan an assembly that loads completely:" + string.Concat(reasons);
    }
}