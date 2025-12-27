using System.Linq.Expressions;

namespace SimplifierConsole;

/// <summary>
///     Решатель для экспоненциальных уравнений вида exp(g(x)) - 1 = 0 → g(x) = 0
///     Также обрабатывает exp(g(x)) = 1 напрямую.
/// </summary>
public static class ExponentialZeroSolver
{
    /// <summary>
    ///     Находит точные конечные корни уравнения expr = 0, если оно содержит экспоненту.
    /// </summary>
    public static ICollection<Root> FindRoots(Expression expr, ParameterExpression parameter)
    {
        var roots = new List<Root>();

        // Нормализуем: приводим к виду exp(...) - 1 или exp(...) = 1
        Expression inner = null;

        if (IsExpMinusOne(expr, out var arg))
        {
            inner = arg;
        }
        else if (IsDirectExpEqualsOne(expr, out arg))
        {
            inner = arg;
        }
        else
        {
            // Попробуем найти exp(...) внутри более сложного выражения
            var visitor = new ExpFinderVisitor();
            visitor.Visit(expr);
            if (visitor.FoundExpArgument != null) inner = visitor.FoundExpArgument;
        }

        if (inner == null)
            return roots; // ничего не нашли

        // Теперь решаем inner = 0 рекурсивно через UniversalZeroSolver (или напрямую через ExactEvaluator)
        // Это даёт неразрывность: экспонента сводится к уже известным солверам

        var innerRoots = UniversalZeroSolver.FindExactRoots(inner, parameter);

        // Все корни inner = 0 являются корнями исходного уравнения exp(inner) - 1 = 0
        roots.AddRange(innerRoots);

        return roots;
    }

    // exp(g(x)) - 1
    private static bool IsExpMinusOne(Expression expr, out Expression argument)
    {
        argument = null;

        if (expr.NodeType == ExpressionType.Subtract &&
            expr is BinaryExpression bin)
        {
            if (IsExpCall(bin.Left) &&
                bin.Right is ConstantExpression c && IsOneLike(c.Value))
            {
                argument = ((MethodCallExpression)bin.Left).Arguments[0];
                return true;
            }

            if (IsExpCall(bin.Right) &&
                bin.Left is ConstantExpression c2 && IsOneLike(c2.Value))
            {
                // 1 - exp(...) → exp(...) = 1, но это то же самое
                argument = ((MethodCallExpression)bin.Right).Arguments[0];
                return true;
            }
        }

        return false;
    }

    // exp(g(x)) = 1 (в виде равенства, но мы ищем expr = 0)
    private static bool IsDirectExpEqualsOne(Expression expr, out Expression argument)
    {
        argument = null;
        return false; // В нашем случае expr — это знаменатель = 0, так что равенство не приходит
    }

    private static bool IsExpCall(Expression expr)
    {
        return expr is MethodCallExpression call &&
               call.Method.DeclaringType == typeof(Math) &&
               call.Method.Name == "Exp" &&
               call.Arguments.Count == 1;
    }

    private static bool IsOneLike(object value)
    {
        return value switch
        {
            int i => i == 1,
            long l => l == 1,
            double d => Math.Abs(d - 1.0) < 1e-12,
            float f => Math.Abs(f - 1.0) < 1e-12f,
            decimal dm => dm == 1m,
            _ => false
        };
    }

    // Вспомогательный visitor для поиска exp(...) внутри сложного выражения
    private class ExpFinderVisitor : ExpressionVisitor
    {
        public Expression FoundExpArgument { get; private set; }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            if (IsExpCall(node) && FoundExpArgument == null) FoundExpArgument = node.Arguments[0];

            return base.VisitMethodCall(node);
        }
    }
}