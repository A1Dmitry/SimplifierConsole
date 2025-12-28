using System.Linq.Expressions;

namespace SimplifierConsole.ZeroSolver
{
    /// <summary>
    /// Решатель для exp(g(x)) - 1 = 0  =>  g(x) = 0
    /// Использует ZeroSolverUtils для уменьшения дублирования.
    /// </summary>
    public static class ExponentialZeroSolver
    {
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
                var visitor = new ExpFinderVisitor();
                visitor.Visit(expr);
                if (visitor.FoundExpArgument != null) inner = visitor.FoundExpArgument;
            }

            if (inner == null)
                return roots;

            // Делегируем поиск корней внутреннему универсальному солверу
            var innerRoots = UniversalZeroSolver.FindExactRoots(inner, parameter);
            if (innerRoots != null)
            {
                foreach (var r in innerRoots) roots.Add(r);
            }

            return roots;
        }

        // Совместимый адаптер: возвращаем первый корень, если он есть.
        public static (ParameterExpression, double)? Solve(Expression expr)
        {
            // используем общий адаптер, чтобы не дублировать поиск параметра и нормализацию
            return ZeroSolverUtils.FindFirstRootFromFindRoots(FindRoots, expr);
        }

        private static bool IsExpMinusOne(Expression expr, out Expression argument)
        {
            argument = null;
            if (expr is BinaryExpression bin && bin.NodeType == ExpressionType.Subtract)
            {
                if (IsExpCall(bin.Left) && bin.Right is ConstantExpression c && IsOneLike(c.Value))
                {
                    argument = ((MethodCallExpression)bin.Left).Arguments[0];
                    return true;
                }

                if (IsExpCall(bin.Right) && bin.Left is ConstantExpression c2 && IsOneLike(c2.Value))
                {
                    argument = ((MethodCallExpression)bin.Right).Arguments[0];
                    return true;
                }
            }

            return false;
        }

        private static bool IsDirectExpEqualsOne(Expression expr, out Expression argument)
        {
            argument = null;
            // оставляю заглушку — при необходимости можно распознавать равенства
            return false;
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
                long l => l == 1L,
                double d => Math.Abs(d - 1.0) == 0,
                float f => Math.Abs(f - 1.0f) == 0,
                decimal dm => dm == 1m,
                _ => false
            };
        }

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
}