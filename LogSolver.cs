using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using SimplifierConsole;
using SimplifierConsole.ZeroSolver;

namespace SimplifierConsole
{
    /// <summary>
    /// Log solver: распознаёт Math.Log(x) == 0 => x = 1.
    /// Реализует FindRoots/FindFirstRoot pattern совместимо с ZeroSolverUtils.
    /// </summary>
    public static class LogSolver
    {
        // Возвращает все точные корни для expr = 0, относительно переданного параметра.
        // Поддерживает случаи вида Log(x) (нативно) и вложенные вызовы, если найдём явный параметр.
        public static ICollection<Root> FindRoots(Expression expr, ParameterExpression parameter)
        {
            var roots = new List<Root>();
            if (expr == null || parameter == null) return roots;

            // Простая форма: Log(x)
            if (expr is MethodCallExpression call && call.Method.DeclaringType == typeof(Math) && call.Method.Name == "Log" && call.Arguments.Count == 1)
            {
                var arg = call.Arguments[0];
                // если аргумент — именно искомый параметр
                if (arg is ParameterExpression p && p == parameter)
                {
                    // ln(x) = 0 => x = 1
                    roots.Add(new Root(parameter, 1.0));
                    return roots;
                }

                // если аргумент — выражение, пытаемся найти, равен ли параметр внутри
                var pf = ZeroSolverUtils.FindFirstParameter(arg);
                if (pf != null && pf == parameter)
                {
                    roots.Add(new Root(parameter, 1.0));
                    return roots;
                }
            }

            // Попытка найти Log(...) внутри более сложного выражения
            var finder = new LogFinder();
            finder.Visit(expr);
            if (finder.FoundLogArgument != null)
            {
                // если внутри нашли Log(u) где u содержит параметр — решаем u = 1
                var inner = finder.FoundLogArgument;
                // Используем UniversalZeroSolver чтобы найти корни inner == 1 => inner-1 == 0
                var eqExpr = Expression.Subtract(inner, Expression.Constant(1.0));
                var innerRoots = UniversalZeroSolver.FindExactRoots(eqExpr, parameter);
                if (innerRoots != null)
                {
                    foreach (var r in innerRoots) roots.Add(r);
                }
            }

            return roots;
        }

        // Совместимый адаптер для SingularitySolver: возвращает первый root (param,double)? используя ZeroSolverUtils
        public static (ParameterExpression, double)? Solve(Expression expr)
        {
            return ZeroSolverUtils.FindFirstRootFromFindRoots(FindRoots, expr);
        }

        private class LogFinder : ExpressionVisitor
        {
            public Expression FoundLogArgument { get; private set; }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (FoundLogArgument == null
                    && node.Method.DeclaringType == typeof(Math)
                    && node.Method.Name == "Log"
                    && node.Arguments.Count == 1)
                {
                    FoundLogArgument = node.Arguments[0];
                    return node;
                }

                return base.VisitMethodCall(node);
            }
        }
    }
}