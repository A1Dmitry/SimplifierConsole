// RicisCore/ExpressionIdentityComparer.cs
using System;
using System.Linq.Expressions;
using System.Reflection;

public static class ExpressionIdentityComparer
{
    /// <summary>
    /// Строгая проверка самоидентичности двух Expression по RICIS L1_IDENTITY: X = X.
    /// Рекурсивное сравнение по структуре дерева.
    /// </summary>
    public static bool AreSelfIdentical(Expression a, Expression b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;

        if (a.NodeType != b.NodeType) return false;
        if (a.Type != b.Type) return false;

        switch (a.NodeType)
        {
            case ExpressionType.Constant:
                return ConstantIdentical((ConstantExpression)a, (ConstantExpression)b);

            case ExpressionType.Parameter:
                return ParameterIdentical((ParameterExpression)a, (ParameterExpression)b);

            case ExpressionType.Add:
            case ExpressionType.Subtract:
            case ExpressionType.Multiply:
            case ExpressionType.Divide:
            case ExpressionType.Power:
            case ExpressionType.And:
            case ExpressionType.Or:
                // все бинарные операции
                return BinaryIdentical((BinaryExpression)a, (BinaryExpression)b);

            case ExpressionType.Negate:
            case ExpressionType.UnaryPlus:
            case ExpressionType.Convert:
                // унарные
                return UnaryIdentical((UnaryExpression)a, (UnaryExpression)b);

            case ExpressionType.Call:
                return MethodCallIdentical((MethodCallExpression)a, (MethodCallExpression)b);

            case ExpressionType.Lambda:
                return LambdaIdentical((LambdaExpression)a, (LambdaExpression)b);

            case ExpressionType.Extension:
                // Для наших RICIS-выражений (InfinityExpression и т.д.)
                return ExtensionIdentical(a, b);

            default:
                return false; // неизвестный тип узла — не идентичны
        }
    }

    private static bool ConstantIdentical(ConstantExpression a, ConstantExpression b)
    {
        return Equals(a.Value, b.Value);
    }

    private static bool ParameterIdentical(ParameterExpression a, ParameterExpression b)
    {
        return a.Name == b.Name && a.Type == b.Type;
    }

    private static bool BinaryIdentical(BinaryExpression a, BinaryExpression b)
    {
        return a.Method == b.Method &&
               AreSelfIdentical(a.Left, b.Left) &&
               AreSelfIdentical(a.Right, b.Right);
    }

    private static bool UnaryIdentical(UnaryExpression a, UnaryExpression b)
    {
        return a.Method == b.Method &&
               AreSelfIdentical(a.Operand, b.Operand);
    }

    private static bool MethodCallIdentical(MethodCallExpression a, MethodCallExpression b)
    {
        if (a.Method != b.Method) return false;

        if (!AreSelfIdentical(a.Object, b.Object)) return false;

        if (a.Arguments.Count != b.Arguments.Count) return false;
        for (int i = 0; i < a.Arguments.Count; i++)
        {
            if (!AreSelfIdentical(a.Arguments[i], b.Arguments[i])) return false;
        }
        return true;
    }

    private static bool LambdaIdentical(LambdaExpression a, LambdaExpression b)
    {
        if (a.Parameters.Count != b.Parameters.Count) return false;
        for (int i = 0; i < a.Parameters.Count; i++)
        {
            if (!ParameterIdentical(a.Parameters[i], b.Parameters[i])) return false;
        }
        return AreSelfIdentical(a.Body, b.Body);
    }

    private static bool ExtensionIdentical(Expression a, Expression b)
    {
        // Для RICIS-расширений (InfinityExpression, BridgedExpression и т.д.)
        // Fallback на ToString() или можно добавить конкретные сравнения
        return a.ToString() == b.ToString();
    }

    /// <summary>
    /// Применяет L1_IDENTITY: если X = X → X/X = 1
    /// </summary>
    public static Expression ApplySelfIdentity(Expression numerator, Expression denominator)
    {
        if (AreSelfIdentical(numerator, denominator))
        {
            return Expression.Constant(1.0, typeof(double));
        }
        return null;
    }
}