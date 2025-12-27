using System;
using System.Numerics;

public readonly struct CircleSectors
{
    public Rational Fraction { get; }

    private CircleSectors(Rational fraction)
    {
        // Используем метод Floor из Rational (добавь его, если нет)
        Rational floor = Rational.Floor(fraction);
        Fraction = fraction - floor;
        if (Fraction < Rational.Zero)
            Fraction += Rational.One;
    }

    public static CircleSectors FromRadians(double radians, int maxDenominator = 100)
    {
        if (double.IsNaN(radians) || double.IsInfinity(radians))
            throw new ArgumentException("Invalid angle");

        double normalized = ((radians % (2 * Math.PI)) + 2 * Math.PI) % (2 * Math.PI);
        double fractionDouble = normalized / (2 * Math.PI);

        Rational best = BestRationalApproximation(fractionDouble, maxDenominator);

        return new CircleSectors(best);
    }

    private static Rational BestRationalApproximation(double x, int maxDenominator)
    {
        if (Math.Abs(x) < 1e-12) return Rational.Zero;
        if (Math.Abs(x - 1.0) < 1e-12) return Rational.One;

        double value = Math.Abs(x);
        BigInteger a0 = (BigInteger)Math.Floor(value);
        double f = value - (double)a0;

        BigInteger p0 = a0, q0 = BigInteger.One;
        BigInteger p1 = BigInteger.One, q1 = BigInteger.Zero;

        BigInteger pBest = p0;
        BigInteger qBest = q0;

        double bestError = Math.Abs(value - (double)pBest / (double)qBest);

        while (q0 <= maxDenominator)
        {
            if (Math.Abs(f) < 1e-12) break;

            double reciprocal = 1.0 / f;
            BigInteger a = (BigInteger)Math.Floor(reciprocal);

            BigInteger pNew = a * p0 + p1;
            BigInteger qNew = a * q0 + q1;

            if (qNew > maxDenominator) break;

            double approx = (double)pNew / (double)qNew;
            double error = Math.Abs(value - approx);

            if (error < bestError)
            {
                bestError = error;
                pBest = pNew;
                qBest = qNew;
            }

            p1 = p0; q1 = q0;
            p0 = pNew; q0 = qNew;

            f = reciprocal - (double)a;
        }

        Rational result = new Rational(pBest, qBest);
        return x < 0 ? -result : result;
    }

    public string InSectors(int totalSectors)
    {
        if (totalSectors <= 0) throw new ArgumentOutOfRangeException(nameof(totalSectors));

        Rational sectorsPassed = Fraction * Rational.Create(totalSectors);
        Rational whole = Rational.Floor(sectorsPassed);
        Rational frac = sectorsPassed - whole;

        if (frac.IsZero)
        {
            if (whole.IsZero) return "0 секторов";
            if (whole == Rational.Create(totalSectors)) return "полный круг";
            return $"ровно {whole} секторов из {totalSectors}";
        }

        return $"{sectorsPassed} секторов из {totalSectors}";
    }

    public override string ToString()
    {
        if (Fraction.IsZero) return "0 круга";
        if (Fraction.Denominator.IsOne) return $"{Fraction.Numerator} полных кругов";
        return $"{Fraction} полного круга";
    }
}