using SimplifierConsole;
using System.Linq.Expressions;

public static class ExpressionStructuralComparer
{
    public static bool AreEqual(Expression a, Expression b)
    {
        if (ReferenceEquals(a, b)) return true;
        if (a is null || b is null) return false;
        if (a.NodeType != b.NodeType) return false;
        if (a.Type != b.Type) return false;

        return a.NodeType switch
        {
            ExpressionType.Constant => ConstantEqual((ConstantExpression)a, (ConstantExpression)b),
            ExpressionType.Parameter => ParameterEqual((ParameterExpression)a, (ParameterExpression)b),
            ExpressionType.Add or ExpressionType.Subtract or ExpressionType.Multiply or ExpressionType.Divide
                => BinaryEqual((BinaryExpression)a, (BinaryExpression)b),
            ExpressionType.Negate or ExpressionType.UnaryPlus or ExpressionType.Convert
                => UnaryEqual((UnaryExpression)a, (UnaryExpression)b),
            ExpressionType.Call => CallEqual((MethodCallExpression)a, (MethodCallExpression)b),
            ExpressionType.Lambda => LambdaEqual((LambdaExpression)a, (LambdaExpression)b),
            ExpressionType.Extension => ExtensionEqual(a, b),
            _ => false
        };
    }

    private static bool ConstantEqual(ConstantExpression a, ConstantExpression b)
        => Equals(a.Value, b.Value);

    private static bool ParameterEqual(ParameterExpression a, ParameterExpression b)
        => a.Name == b.Name && a.Type == b.Type;

    private static bool BinaryEqual(BinaryExpression a, BinaryExpression b)
        => a.Method == b.Method &&
           AreEqual(a.Left, b.Left) &&
           AreEqual(a.Right, b.Right);

    private static bool UnaryEqual(UnaryExpression a, UnaryExpression b)
        => a.Method == b.Method &&
           AreEqual(a.Operand, b.Operand);

    private static bool CallEqual(MethodCallExpression a, MethodCallExpression b)
    {
        if (a.Method != b.Method) return false;
        if (!AreEqual(a.Object, b.Object)) return false;
        if (a.Arguments.Count != b.Arguments.Count) return false;

        for (int i = 0; i < a.Arguments.Count; i++)
            if (!AreEqual(a.Arguments[i], b.Arguments[i])) return false;

        return true;
    }

    private static bool LambdaEqual(LambdaExpression a, LambdaExpression b)
    {
        if (a.Parameters.Count != b.Parameters.Count) return false;

        for (int i = 0; i < a.Parameters.Count; i++)
            if (!ParameterEqual(a.Parameters[i], b.Parameters[i])) return false;

        return AreEqual(a.Body, b.Body);
    }

    // === RICIS EXTENSIONS ===
    private static bool ExtensionEqual(Expression a, Expression b)
    {
        return (a, b) switch
        {
            (InfinityExpression ia, InfinityExpression ib)
                => InfinityEqual(ia, ib),

            (SingularityMonolithExpression ma, SingularityMonolithExpression mb)
                => MonolithEqual(ma, mb),

            (BridgedExpression ba, BridgedExpression bb)
                => BridgedEqual(ba, bb),

            (var xa, var xb)
                => xa.GetType() == xb.GetType() // fallback: same type
        };
    }

    private static bool InfinityEqual(InfinityExpression a, InfinityExpression b)
        => a.SingularityValue == b.SingularityValue &&
           ParameterEqual(a.Variable, b.Variable) &&
           AreEqual(a.Numerator, b.Numerator);

    private static bool MonolithEqual(SingularityMonolithExpression a, SingularityMonolithExpression b)
    {
        if (a.Singularities.Count != b.Singularities.Count) return false;

        for (int i = 0; i < a.Singularities.Count; i++)
            if (!InfinityEqual(a.Singularities[i], b.Singularities[i])) return false;

        return true;
    }

    private static bool BridgedEqual(BridgedExpression a, BridgedExpression b)
        => a.SingularityValue == b.SingularityValue &&
           ParameterEqual(a.Variable, b.Variable) &&
           AreEqual(a.Content, b.Content);
}
