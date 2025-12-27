using System.Linq.Expressions;

namespace SimplifierConsole;

public static class PolynomialLongDivision
{
    public static Expression TryDivide(Expression numerator, Expression denominator, ParameterExpression param)
    {
        var numCollector = new PolynomialCoefficientCollector(param);
        numCollector.Visit(numerator);
        if (!numCollector.IsPolynomial) return null;

        var denCollector = new PolynomialCoefficientCollector(param);
        denCollector.Visit(denominator);
        if (!denCollector.IsPolynomial) return null;

        // Используем SortedDictionary с обратным порядком для удобства
        var dividend = new SortedDictionary<int, Rational>(numCollector.Coefficients,
            Comparer<int>.Create((x, y) => y.CompareTo(x)));
        var divisor = new SortedDictionary<int, Rational>(denCollector.Coefficients,
            Comparer<int>.Create((x, y) => y.CompareTo(x)));

        if (divisor.Count == 0) return null;

        var divisorDegree = divisor.Keys.First(); // максимальная степень
        var leadingDivisor = divisor[divisorDegree];
        if (leadingDivisor.IsZero) return null;

        var quotient = Divide(dividend, divisor, divisorDegree, leadingDivisor);
        if (quotient == null) return null;

        return BuildExpressionFromCoefficients(quotient, param);
    }

    private static Dictionary<int, Rational> Divide(
        SortedDictionary<int, Rational> dividend,
        SortedDictionary<int, Rational> divisor,
        int divisorDegree,
        Rational leadingDivisor)
    {
        var quotient = new Dictionary<int, Rational>();
        var remainder = new SortedDictionary<int, Rational>(dividend, Comparer<int>.Create((x, y) => y.CompareTo(x)));

        while (remainder.Count > 0 && remainder.Keys.First() >= divisorDegree)
        {
            var currentDegree = remainder.Keys.First();
            var leadingDividend = remainder[currentDegree];

            var termCoeff = leadingDividend / leadingDivisor;
            var termDegree = currentDegree - divisorDegree;

            quotient[termDegree] = termCoeff;

            // Вычитаем term * divisor
            foreach (var (degB, coeffB) in divisor)
            {
                var degResult = degB + termDegree;
                var subtract = termCoeff * coeffB;

                if (remainder.TryGetValue(degResult, out var current))
                {
                    var newCoeff = current - subtract;
                    if (newCoeff.IsZero)
                        remainder.Remove(degResult);
                    else
                        remainder[degResult] = newCoeff;
                }
                else
                {
                    remainder[degResult] = -subtract;
                }
            }
        }

        // Если остаток не нулевой — возвращаем null
        if (remainder.Count > 0 && remainder.Values.Any(v => !v.IsZero))
            return null;

        return quotient;
    }

    private static Expression BuildExpressionFromCoefficients(Dictionary<int, Rational> coeffs,
        ParameterExpression param)
    {
        if (coeffs.Count == 0) return Expression.Constant(0.0);

        Expression result = null;

        // Сортируем по убыванию степени
        foreach (var kv in coeffs.OrderByDescending(k => k.Key))
        {
            var degree = kv.Key;
            var coeff = kv.Value;

            Expression term;

            if (coeff.IsZero) continue;

            Expression coeffExpr = ConstantFromRational(coeff);

            if (degree == 0)
            {
                term = coeffExpr;
            }
            else if (degree == 1)
            {
                term = Expression.Multiply(coeffExpr, param);
            }
            else
            {
                Expression power = param;
                for (var i = 1; i < degree; i++) power = Expression.Multiply(power, param);
                term = Expression.Multiply(coeffExpr, power);
            }

            result = result == null ? term : Expression.Add(result, term);
        }

        return result ?? Expression.Constant(0.0);
    }

    private static ConstantExpression ConstantFromRational(Rational r)
    {
        var value = r.ToDouble();
        return Expression.Constant(value);
    }
}