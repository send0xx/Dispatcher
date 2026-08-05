namespace Dispatcher;

/// <summary>
/// Represents the absence of a meaningful response value.
/// </summary>
public readonly struct Unit : IEquatable<Unit>, IComparable<Unit>, IComparable
{
    private static readonly Unit _value = new();

    /// <summary>
    /// Gets the single logical <see cref="Unit"/> value.
    /// </summary>
    public static ref readonly Unit Value => ref _value;

    /// <summary>
    /// Gets a completed value task containing <see cref="Value"/>.
    /// </summary>
    public static ValueTask<Unit> ValueTask => new(_value);

    /// <inheritdoc />
    public int CompareTo(Unit other) => 0;

    int IComparable.CompareTo(object? obj) => 0;

    /// <inheritdoc />
    public override int GetHashCode() => 0;

    /// <inheritdoc />
    public bool Equals(Unit other) => true;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Unit;

    /// <summary>
    /// Determines whether two <see cref="Unit"/> values are equal.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>Always <see langword="true"/>.</returns>
    public static bool operator ==(Unit left, Unit right) => true;

    /// <summary>
    /// Determines whether two <see cref="Unit"/> values are unequal.
    /// </summary>
    /// <param name="left">The left value.</param>
    /// <param name="right">The right value.</param>
    /// <returns>Always <see langword="false"/>.</returns>
    public static bool operator !=(Unit left, Unit right) => false;

    /// <inheritdoc />
    public override string ToString() => "()";
}