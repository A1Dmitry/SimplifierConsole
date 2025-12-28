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
                if (ReferenceEquals(expr, p)) roots.Add((p, 0.0));
                break;

            case BinaryExpression bin:
                if (bin.NodeType == ExpressionType.Multiply)
                {
                    // Только для произведений рекурсивно ищем факторы
                    CollectRoots(bin.Left, roots);
                    CollectRoots(bin.Right, roots);
                    return;
                }

                if (bin.NodeType == ExpressionType.Subtract)
                    // Обработка x^n - 1 = 0
                    if (bin.Right is ConstantExpression constRight &&
                        constRight.Value is double rightVal &&
                        Math.Abs(rightVal - 1.0) < double.Epsilon)
                        // Левый операнд — степень x
                        if (TryExtractPower(bin.Left, out var baseExpr, out var exponent))
                            if (baseExpr is ParameterExpression param && exponent > 1)
                            {
                                // Корни n-й степени из 1 — только вещественные
                                // Для чётного n: x = 1 и x = -1
                                // Для нечётного n: только x = 1
                                roots.Add((param, 1.0));
                                if (exponent % 2 == 0) roots.Add((param, -1.0));
                            }

                if (bin.NodeType is ExpressionType.Subtract or ExpressionType.Add)
                {
                    // Линейные формы
                    if (TryExtractLinear(bin, out var paramLinear, out var a, out var b))
                        if (Math.Abs(a) > 1e-10)
                        {
                            var rootVal = bin.NodeType == ExpressionType.Subtract ? b / a : -b / a;
                            roots.Add((paramLinear, rootVal));
                        }

                    // Квадратичные формы x² ± ... = 0
                    if (bin.Right is ConstantExpression constRight && TryGetDouble(constRight, out var cValue))
                    {
                        var sign = bin.NodeType == ExpressionType.Subtract ? -1.0 : 1.0;
                        var effectiveC = sign * cValue;

                        if (PolynomialParser.ParseQuadratic(bin.Left) is var quad && quad.HasValue)
                        {
                            var (param, aQuad, bQuad, _) = quad.Value;
                            if (Math.Abs(aQuad) > double.Epsilon)
                            {
                                var discriminant = bQuad * bQuad - 4 * aQuad * effectiveC;
                                if (discriminant >= 0)
                                {
                                    var sqrtD = Math.Sqrt(discriminant);
                                    roots.Add((param, (-bQuad + sqrtD) / (2 * aQuad)));
                                    if (discriminant > 0) roots.Add((param, (-bQuad - sqrtD) / (2 * aQuad)));
                                }
                            }
                        }
                    }
                }

                // Рекурсия убрана — лишние x=0 больше не добавляются
                break;

            case MethodCallExpression call when call.Method.Name == "Log":
                if (call.Arguments.Count == 1 && call.Arguments[0] is ParameterExpression paramLog)
                    roots.Add((paramLog, 1.0));
                break;
        }
    }

    private static bool TryExtractPower(Expression expr, out Expression baseExpr, out int exponent)
    {
        baseExpr = null;
        exponent = 0;

        // Math.Pow(x, n)
        if (expr is MethodCallExpression pow && pow.Method.Name == "Pow" && pow.Arguments.Count == 2)
        {
            baseExpr = pow.Arguments[0];
            if (pow.Arguments[1] is ConstantExpression constExp && constExp.Value is double d)
            {
                exponent = (int)d;
                return exponent == d && exponent > 0;
            }
        }

        // Развёрнутая степень (x * x * x * x) — считаем количество умножений
        var count = CountMultiplications(expr);
        if (count > 1 && IsParameterMultiplication(expr))
        {
            baseExpr = GetParameterFromMultiplication(expr);
            exponent = count;
            return true;
        }

        return false;
    }

    private static int CountMultiplications(Expression expr)
    {
        if (expr is BinaryExpression bin && bin.NodeType == ExpressionType.Multiply)
            return CountMultiplications(bin.Left) + CountMultiplications(bin.Right);
        return expr is ParameterExpression ? 1 : 0;
    }

    private static bool IsParameterMultiplication(Expression expr)
    {
        if (expr is ParameterExpression) return true;
        if (expr is BinaryExpression bin && bin.NodeType == ExpressionType.Multiply)
            return IsParameterMultiplication(bin.Left) && IsParameterMultiplication(bin.Right);
        return false;
    }

    private static ParameterExpression GetParameterFromMultiplication(Expression expr)
    {
        if (expr is ParameterExpression p) return p;
        if (expr is BinaryExpression bin && bin.NodeType == ExpressionType.Multiply)
            return GetParameterFromMultiplication(bin.Left) ?? GetParameterFromMultiplication(bin.Right);
        return null;
    }


    private static bool TryExtractLinear(BinaryExpression expr, out ParameterExpression param, out double a,
        out double b)
    {
        param = null;
        a = 1.0;
        b = 0.0;

        // 2*x - 6
        if (expr.Left is BinaryExpression mul && mul.NodeType == ExpressionType.Multiply &&
            mul.Left is ConstantExpression c && mul.Right is ParameterExpression p)
            if (TryGetDouble(c, out a))
            {
                param = p;
                if (TryGetDouble(expr.Right as ConstantExpression, out b))
                    return true;
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
        if (c.Value is double d)
        {
            val = d;
            return true;
        }

        if (c.Value is int i)
        {
            val = i;
            return true;
        }

        if (c.Value is float f)
        {
            val = f;
            return true;
        }

        return false;
    }
}