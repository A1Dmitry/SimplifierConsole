using System.Linq.Expressions;

namespace SimplifierConsole.ZeroSolver;

public static class PolynomialZeroSolver
{
    public static List<Root> FindRoots(Expression expr, ParameterExpression param)
    {
        var collector = new PolynomialCoefficientCollector(param);
        collector.Visit(expr);

        if (!collector.IsPolynomial || collector.Coefficients.Count == 0 ||
            collector.Coefficients.All(c => c.Value.IsZero))
            return new List<Root>();

        var degree = collector.Coefficients.Keys.Max();
        if (degree == 0) return new List<Root>(); // константа ≠0

        // Теорема о рациональных корнях
        var possibleRationals = RationalRootTheorem.GetPossibleRoots(collector.Coefficients);

        var roots = new List<Root>();

        foreach (var candidate in possibleRationals)
            if (ExactEvaluator.TryEvaluate(expr, param.Name, candidate, out var result) && result.IsZero)
                roots.Add(new Root(param, candidate));

        return roots;
    }
}