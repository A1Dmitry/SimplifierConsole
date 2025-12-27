using System.Numerics;

internal class RationalRootTheorem
{
    public static List<Rational> GetPossibleRoots(Dictionary<int, Rational> coeffs)
    {
        var constant = coeffs[0];
        var leading = coeffs[coeffs.Keys.Max()];

        var pFactors = Factorize(constant.Numerator);
        var qFactors = Factorize(leading.Denominator);

        var candidates = new HashSet<Rational>();

        foreach (var p in pFactors)
            foreach (var q in qFactors)
            {
                candidates.Add(new Rational(p, q));
                candidates.Add(new Rational(BigInteger.Negate(p), q));
            }

        return candidates.OrderBy(r => r.ToDouble()).ToList();
    }

    private static List<BigInteger> Factorize(BigInteger n)
    {
        n = BigInteger.Abs(n);
        var factors = new List<BigInteger> { BigInteger.One };

        for (BigInteger i = 2; i * i <= n; i++)
        {
            if (n % i == 0)
            {
                factors.Add(i);
                while (n % i == 0) n /= i;
            }
        }
        if (n > 1) factors.Add(n);

        return factors;
    }
}