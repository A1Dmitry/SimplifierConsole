// AlgebraicSimplifier.cs (исправленная версия)

using System.Linq.Expressions;

namespace SimplifierConsole.Simplifiers;

public static class AlgebraicSimplifier
{
    public static Expression CleanFirst(Expression expr)
    {
        if (expr == null) return null;

        // Найти параметр (x)
        var paramFinder = new ParameterFinder();
        paramFinder.Visit(expr);
        var param = paramFinder.FoundParameter;

        if (param != null)
        {
            // Заменяем Pow(x, n) на x*x*...
            expr = PowToMultiplicationVisitor.ReplacePow(expr, param);
        }

        return new AlgebraicReductionVisitor().Visit(expr);
    }

    public static Expression ApplyPostRicis(Expression expr)
    {
        return expr; // пока ничего не делаем
    }

    private class AlgebraicReductionVisitor : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);

            if (node.NodeType == ExpressionType.Divide)
            {
                if (left.AsComparable() == right.AsComparable())
                {
                    return Expression.Constant(1.0, typeof(double));
                }
                

                var parameter = FindSingleParameter(node);
                if (parameter != null)
                {
                    var divided = PolynomialLongDivision.TryDivide(left, right, parameter);
                    if (divided != null)
                    {
                        return Visit(divided);
                    }
                }
            }

            // Безопасное создание бинарного узла
            if (left == node.Left && right == node.Right)
                return node;

            return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
        }

        // Добавляем обработку вызовов методов (Sin, Cos, Exp и т.д.)
        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            var obj = Visit(node.Object);
            var args = node.Arguments.Select(Visit);

            if (obj == node.Object && args.SequenceEqual(node.Arguments))
                return node;

            return Expression.Call(obj, node.Method, args);
        }

        // Фолбэк для всех остальных узлов
        protected override Expression VisitExtension(Expression node)
        {
            return node; // InfinityExpression и т.д. не трогаем здесь
        }

        private static ParameterExpression FindSingleParameter(Expression expr)
        {
            var finder = new ParameterFinder();
            finder.Visit(expr);
            return finder.FoundParameter;
        }
    }
}