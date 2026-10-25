using System.Globalization;
using System.Numerics;

namespace Apportionment.Core.Arithmetic;

/// <summary>An immutable, reduced fraction with a positive denominator.</summary>
public sealed record Rational : IComparable<Rational>
{
    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; }

    public static Rational Zero { get; } = new(0, 1);

    public Rational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
            throw new DivideByZeroException("A rational denominator cannot be zero.");

        if (denominator.Sign < 0)
            (numerator, denominator) = (-numerator, -denominator);

        var gcd = BigInteger.GreatestCommonDivisor(numerator, denominator);
        Numerator = numerator / gcd;
        Denominator = denominator / gcd;
    }

    public int CompareTo(Rational? other) => other is null ? 1 :
        (Numerator * other.Denominator).CompareTo(other.Numerator * Denominator);

    public Rational Abs() => new(BigInteger.Abs(Numerator), Denominator);

    public static Rational operator +(Rational left, Rational right) => new(
        left.Numerator * right.Denominator + right.Numerator * left.Denominator,
        left.Denominator * right.Denominator);

    public static Rational operator -(Rational left, Rational right) => new(
        left.Numerator * right.Denominator - right.Numerator * left.Denominator,
        left.Denominator * right.Denominator);

    public override string ToString() => Denominator.IsOne
        ? Numerator.ToString(CultureInfo.InvariantCulture)
        : string.Create(CultureInfo.InvariantCulture, $"{Numerator}/{Denominator}");

    /// <summary>Rounds halves away from zero, using integer arithmetic only.</summary>
    public string ToDecimalString(int places = 3)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(places);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(places, 100);
        var scale = BigInteger.Pow(10, places);
        var rounded = BigInteger.DivRem(BigInteger.Abs(Numerator) * scale, Denominator, out var remainder);
        if (remainder * 2 >= Denominator)
            rounded++;

        var sign = Numerator.Sign < 0 && !rounded.IsZero ? "-" : "";
        var digits = rounded.ToString(CultureInfo.InvariantCulture).PadLeft(places + 1, '0');
        return places == 0 ? sign + digits : $"{sign}{digits[..^places]}.{digits[^places..]}";
    }
}
