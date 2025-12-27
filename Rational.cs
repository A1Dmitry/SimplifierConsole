using System.Numerics;
using DivideByZeroException = System.DivideByZeroException;

public readonly struct Rational : IEquatable<Rational>
{
    public static readonly Rational Zero = new(0);
    public static readonly Rational One = new(1);

    public BigInteger Numerator { get; }
    public BigInteger Denominator { get; } // always > 0

    public static Rational operator -(Rational a)
    {
        if (a.Numerator.IsZero)
            return a; // -0 = 0

        return new Rational(-a.Numerator, a.Denominator);
    }

    public Rational(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero) throw new DivideByZeroException();
        if (denominator.Sign < 0)
        {
            numerator = BigInteger.Negate(numerator);
            denominator = BigInteger.Negate(denominator);
        }

        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(numerator), denominator);
        Numerator = numerator / gcd;
        Denominator = denominator / gcd;
    }

    public Rational(BigInteger integer) : this(integer, BigInteger.One)
    {
    }

    public bool IsZero => Numerator.IsZero;

    public static Rational Create(long value)
    {
        return new Rational(value);
    }

    public static Rational FromDecimal(decimal d)
    {
        var bits = decimal.GetBits(d);
        var lo = (uint)bits[0];
        var mid = (uint)bits[1];
        var hi = (uint)bits[2];
        var flags = (uint)bits[3];
        var sign = (flags & 0x80000000) != 0 ? -1 : 1;
        var scale = (flags >> 16) & 0x7F;

        var numerator = ((BigInteger)hi << 64) | ((BigInteger)mid << 32) | lo;
        numerator *= sign;
        var denominator = BigInteger.Pow(10, (int)scale);
        return new Rational(numerator, denominator);
    }

    public double ToDouble()
    {
        return (double)Numerator / (double)Denominator;
    }

    public static Rational operator +(Rational a, Rational b)
    {
        return new Rational(a.Numerator * b.Denominator + b.Numerator * a.Denominator, a.Denominator * b.Denominator);
    }

    public static Rational operator -(Rational a, Rational b)
    {
        return new Rational(a.Numerator * b.Denominator - b.Numerator * a.Denominator, a.Denominator * b.Denominator);
    }

    public static Rational operator *(Rational a, Rational b)
    {
        return new Rational(a.Numerator * b.Numerator, a.Denominator * b.Denominator);
    }

    public static Rational operator /(Rational a, Rational b)
    {
        if (b.Numerator.IsZero) throw new DivideByZeroException();
        return new Rational(a.Numerator * b.Denominator, a.Denominator * b.Numerator);
    }

    public override string ToString()
    {
        return Denominator.IsOne ? Numerator.ToString() : $"{Numerator}/{Denominator}";
    }

    public bool Equals(Rational other)
    {
        return Numerator.Equals(other.Numerator) && Denominator.Equals(other.Denominator);
    }


    public static Rational Floor(Rational r)
    {
        var floored = r.Numerator / r.Denominator;
        if (r.Numerator < 0 && r.Numerator % r.Denominator != 0)
            floored -= BigInteger.One;
        return new Rational(floored);
    }

    public static bool operator <(Rational left, Rational right)
    {
        return left.Numerator * right.Denominator < right.Numerator * left.Denominator;
    }

    public static bool operator ==(Rational left, Rational right)
    {
        return left.Numerator * right.Denominator == right.Numerator * left.Denominator;
    }

    public static bool operator !=(Rational left, Rational right)
    {
        return !(left == right);
    }


    public static bool operator >(Rational left, Rational right)
    {
        return left.Numerator * right.Denominator > right.Numerator * left.Denominator;
    }

    public static bool operator <=(Rational left, Rational right)
    {
        return left.Numerator * right.Denominator <= right.Numerator * left.Denominator;
    }

    public static bool operator >=(Rational left, Rational right)
    {
        return left.Numerator * right.Denominator >= right.Numerator * left.Denominator;
    }
}