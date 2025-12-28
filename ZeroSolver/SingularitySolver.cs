// SingularitySolver.cs (финальная версия)

using System.Linq.Expressions;

namespace SimplifierConsole.ZeroSolver;

public static class SingularitySolver
{
    public static List<(ParameterExpression, double)> SolveRoot(Expression denominator)
    {
        var roots = new HashSet<(ParameterExpression, double)>();
        CollectRoots(denominator, roots);
        return roots.ToList();
    }

    private static void CollectRoots(Expression expr, HashSet<(ParameterExpression Parameter, double Value)> roots)
    {
        switch (expr)
        {
            case ParameterExpression p:
                // Только если весь знаменатель — чистая переменная x (1/x)
                if (ReferenceEquals(expr, p))
                {
                    roots.Add((p, 0.0));
                }
                break;

            case BinaryExpression bin:
                if (bin.NodeType == ExpressionType.Multiply)
                {
                    // Только для произведений рекурсивно ищем факторы
                    CollectRoots(bin.Left, roots);
                    CollectRoots(bin.Right, roots);
                    return;
                }

                if (bin.NodeType is ExpressionType.Subtract or ExpressionType.Add)
                {
                    // Линейные формы
                    if (TryExtractLinear(bin, out var paramLinear, out var a, out var b))
                    {
                        if (Math.Abs(a) > 1e-10)
                        {
                            double rootVal = bin.NodeType == ExpressionType.Subtract ? b / a : -b / a;
                            roots.Add((paramLinear, rootVal));
                        }
                    }

                    // Квадратичные формы x² ± ... = 0
                    if (bin.Right is ConstantExpression constRight && TryGetDouble(constRight, out var cValue))
                    {
                        double sign = bin.NodeType == ExpressionType.Subtract ? -1.0 : 1.0;
                        double effectiveC = sign * cValue;

                        if (PolynomialParser.ParseQuadratic(bin.Left) is var quad && quad.HasValue)
                        {
                            var (param, aQuad, bQuad, _) = quad.Value;
                            if (Math.Abs(aQuad) > double.Epsilon)
                            {
                                double discriminant = bQuad * bQuad - 4 * aQuad * effectiveC;
                                if (discriminant >= 0)
                                {
                                    double sqrtD = Math.Sqrt(discriminant);
                                    roots.Add((param, (-bQuad + sqrtD) / (2 * aQuad)));
                                    if (discriminant > 0)
                                    {
                                        roots.Add((param, (-bQuad - sqrtD) / (2 * aQuad)));
                                    }
                                }
                            }
                        }
                    }
                }
                // Рекурсия убрана — лишние x=0 больше не добавляются
                break;

            case MethodCallExpression call when call.Method.Name == "Log":
                if (call.Arguments.Count == 1 && call.Arguments[0] is ParameterExpression paramLog)
                {
                    roots.Add((paramLog, 1.0));
                }
                break;

            default:
                break;
        }
    }



    private static bool TryExtractLinear(BinaryExpression expr, out ParameterExpression param, out double a, out double b)
    {
        param = null; a = 1.0; b = 0.0;

        // 2*x - 6
        if (expr.Left is BinaryExpression mul && mul.NodeType == ExpressionType.Multiply &&
            mul.Left is ConstantExpression c && mul.Right is ParameterExpression p)
        {
            if (TryGetDouble(c, out a))
            {
                param = p;
                if (TryGetDouble(expr.Right as ConstantExpression, out b))
                    return true;
            }
        }

        // x - 2
        if (expr.Left is ParameterExpression p2 && expr.Right is ConstantExpression c2)
        {
            param = p2;
            a = 1.0;
            if (TryGetDouble(c2, out b))
                return true;
        }

        return false;
    }

    private static bool TryGetDouble(ConstantExpression c, out double val)
    {
        val = 0.0;
        if (c?.Value == null) return false;
        if (c.Value is double d) { val = d; return true; }
        if (c.Value is int i) { val = i; return true; }
        if (c.Value is float f) { val = f; return true; }
        return false;
    }
}