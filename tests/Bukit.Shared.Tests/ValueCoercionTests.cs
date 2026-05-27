using Bukit.Shared;
using Xunit;

namespace Bukit.Shared.Tests;

public sealed class ValueCoercionTests
{
    [Fact]
    public void IsTruthy_Null_ReturnsFalse()
    {
        Assert.False(ValueCoercion.IsTruthy(null));
    }

    [Fact]
    public void IsTruthy_BooleanTrue_ReturnsTrue()
    {
        Assert.True(ValueCoercion.IsTruthy(true));
    }

    [Fact]
    public void IsTruthy_BooleanFalse_ReturnsFalse()
    {
        Assert.False(ValueCoercion.IsTruthy(false));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("yes")]
    [InlineData("Yes")]
    [InlineData("YES")]
    [InlineData("1")]
    [InlineData("on")]
    [InlineData("On")]
    [InlineData("ON")]
    public void IsTruthy_TruthyStrings_ReturnsTrue(string input)
    {
        Assert.True(ValueCoercion.IsTruthy(input));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("no")]
    [InlineData("0")]
    [InlineData("off")]
    [InlineData("anything-else")]
    [InlineData("")]
    [InlineData("   ")]
    public void IsTruthy_NonTruthyStrings_ReturnsFalse(string input)
    {
        Assert.False(ValueCoercion.IsTruthy(input));
    }

    [Fact]
    public void IsTruthy_EmptyOrWhitespace_ReturnsFalse()
    {
        Assert.False(ValueCoercion.IsTruthy(""));
        Assert.False(ValueCoercion.IsTruthy("   "));
    }

    [Fact]
    public void IsTruthy_IntegerZero_ReturnsFalse()
    {
        Assert.False(ValueCoercion.IsTruthy(0));
    }

    [Fact]
    public void IsTruthy_IntegerNonZero_ReturnsFalse()
    {
        Assert.False(ValueCoercion.IsTruthy(42));
    }

    [Fact]
    public void IsFalsy_Null_ReturnsTrue()
    {
        Assert.True(ValueCoercion.IsFalsy(null));
    }

    [Fact]
    public void IsFalsy_BooleanFalse_ReturnsTrue()
    {
        Assert.True(ValueCoercion.IsFalsy(false));
    }

    [Fact]
    public void IsFalsy_BooleanTrue_ReturnsFalse()
    {
        Assert.False(ValueCoercion.IsFalsy(true));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("False")]
    [InlineData("FALSE")]
    [InlineData("no")]
    [InlineData("No")]
    [InlineData("NO")]
    [InlineData("0")]
    [InlineData("off")]
    [InlineData("Off")]
    [InlineData("OFF")]
    public void IsFalsy_FalsyStrings_ReturnsTrue(string input)
    {
        Assert.True(ValueCoercion.IsFalsy(input));
    }

    [Theory]
    [InlineData("true")]
    [InlineData("yes")]
    [InlineData("1")]
    [InlineData("on")]
    [InlineData("anything-else")]
    public void IsFalsy_NonFalsyStrings_ReturnsFalse(string input)
    {
        Assert.False(ValueCoercion.IsFalsy(input));
    }

    [Fact]
    public void IsFalsy_EmptyOrWhitespace_ReturnsTrue()
    {
        Assert.True(ValueCoercion.IsFalsy(""));
        Assert.True(ValueCoercion.IsFalsy("   "));
    }

    [Fact]
    public void ToBooleanOrNull_Truthy_ReturnsTrue()
    {
        Assert.True(ValueCoercion.ToBooleanOrNull("true"));
        Assert.True(ValueCoercion.ToBooleanOrNull("1"));
        Assert.True(ValueCoercion.ToBooleanOrNull(true));
    }

    [Fact]
    public void ToBooleanOrNull_Falsy_ReturnsFalse()
    {
        Assert.False(ValueCoercion.ToBooleanOrNull("false"));
        Assert.False(ValueCoercion.ToBooleanOrNull("0"));
        Assert.False(ValueCoercion.ToBooleanOrNull(false));
        Assert.False(ValueCoercion.ToBooleanOrNull(null));
    }

    [Fact]
    public void ToBooleanOrNull_Neither_ReturnsNull()
    {
        Assert.Null(ValueCoercion.ToBooleanOrNull("maybe"));
        Assert.Null(ValueCoercion.ToBooleanOrNull(42));
    }
}
