// StandardOperationsPhase.cs
using System.Linq.Expressions;

namespace SimplifierConsole;

/// <summary>
/// Phase 5: Standard operations (по RICIS 7.3)
/// Применяет безопасные стандартные упрощения после RICIS
/// DRY-подход: минимальный визитор только для очевидных случаев
/// Не трогает ∞_F, Monolith и другие кастомные узлы
/// </summary>
public static class StandardOperationsPhase
{
    public static Expression Apply(Expression expr)
    {
        if (expr == null) return null;
        return new StandardOperationsVisitor().Visit(expr);
    }

    private class StandardOperationsVisitor : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = Visit(node.Left);
            var right = Visit(node.Right);

            // 1 * x → x
            if (node.NodeType == ExpressionType.Multiply)
            {
                if (IsConstantOne(left)) return right;
                if (IsConstantOne(right)) return left;
            }

            // 0 + x → x
            if (node.NodeType == ExpressionType.Add)
            {
                if (IsConstantZero(left)) return right;
                if (IsConstantZero(right)) return left;
            }

            // x + 0 → x, x * 1 → x (уже покрыто выше)

            if (left == node.Left && right == node.Right)
                return node;

            return Expression.MakeBinary(node.NodeType, left, right, node.IsLiftedToNull, node.Method);
        }

        protected override Expression VisitExtension(Expression node)
        {
            // Не трогаем RICIS-узлы: ∞_F, Monolith, Bridged
            return node;
        }

        private static bool IsConstantOne(Expression expr)
        {
            return expr is ConstantExpression c &&
                   c.Value is double d &&
                   Math.Abs(d - 1.0) < double.Epsilon;
        }

        private static bool IsConstantZero(Expression expr)
        {
            return expr is ConstantExpression c &&
                   c.Value is double d &&
                   Math.Abs(d) < double.Epsilon;
        }
    }
}