using System.Runtime.CompilerServices;
using Xunit;

namespace Dispatcher.DependencyInjection.Tests.Contracts;

public sealed class UnitTests
{
    [Fact]
    public void All_values_are_equal()
    {
        var left = Unit.Value;
        var right = new Unit();

        Assert.True(left == right);
        Assert.False(left != right);
        Assert.True(left.Equals(right));
        Assert.True(left.Equals((object)right));
        Assert.Equal(left.GetHashCode(), right.GetHashCode());
    }

    [Fact]
    public void Equality_with_a_non_unit_value_is_false()
    {
        var value = Unit.Value;
        // Held as object so the object overload is what runs: the point is that a boxed value of an
        // unrelated type fails the type check inside it.
        object text = "()";
        object number = 0;

        Assert.False(value.Equals(null));
        Assert.False(value.Equals(text));
        Assert.False(value.Equals(number));
    }

    [Fact]
    public void Values_never_order_relative_to_each_other()
    {
        var value = Unit.Value;

        Assert.Equal(0, value.CompareTo(new Unit()));
        Assert.Equal(0, ((IComparable)value).CompareTo(new Unit()));
    }

    [Fact]
    public void Relational_operators_agree_that_no_value_precedes_or_follows_another()
    {
        var left = Unit.Value;
        var right = new Unit();

        Assert.False(left < right);
        Assert.False(left > right);
        Assert.True(left <= right);
        Assert.True(left >= right);
    }

    [Fact]
    public void Value_hands_out_one_shared_instance_by_reference()
    {
        // Value is declared ref readonly so that reading it never copies the struct. Comparing the
        // references proves that declaration, which a plain get-only property would not keep.
        ref readonly var first = ref Unit.Value;
        ref readonly var second = ref Unit.Value;

        Assert.True(Unsafe.AreSame(in first, in second));
    }

    [Fact]
    public async Task ValueTask_completes_synchronously_with_the_unit_value()
    {
        var task = Unit.ValueTask;

        Assert.True(task.IsCompletedSuccessfully);
        Assert.Equal(Unit.Value, await task);
    }

    [Fact]
    public void ToString_returns_the_empty_tuple_literal()
    {
        Assert.Equal("()", Unit.Value.ToString());
    }
}