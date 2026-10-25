using System.Numerics;
using Apportionment.Core.Arithmetic;

namespace Apportionment.Tests;

public class RationalTests
{
    [Fact]
    public void FractionsAreNormalizedAndComparableWithoutPrecisionLoss()
    {
        Assert.Equal(new Rational(1, 2), new Rational(-2, -4));
        Assert.Equal(Rational.Zero, new Rational(0, -123));
        Assert.Equal(new Rational(-1, 2), new Rational(1, -2));
        var huge = BigInteger.Pow(10, 100);
        Assert.True(new Rational(huge + 1, huge).CompareTo(new Rational(1, 1)) > 0);
        Assert.Equal(new Rational(5, 6), new Rational(1, 2) + new Rational(1, 3));
        Assert.Equal(new Rational(1, 6), new Rational(1, 2) - new Rational(1, 3));
        Assert.Throws<DivideByZeroException>(() => new Rational(1, 0));
    }

    [Theory]
    [InlineData(1, 3, 3, "0.333")]
    [InlineData(-1, 2, 0, "-1")]
    [InlineData(1999, 2000, 3, "1.000")]
    [InlineData(-1, 10000, 3, "0.000")]
    public void DecimalDisplayRoundsExactly(int numerator, int denominator, int places, string expected)
    {
        Assert.Equal(expected, new Rational(numerator, denominator).ToDecimalString(places));
    }
}
