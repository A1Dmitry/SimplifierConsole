using System.Linq.Expressions;

namespace SimplifierConsole.Simplifiers;

public static class TrigonometricSimplifier
{
    public static Expression TrySimplify(Expression numerator, Expression denominator, ParameterExpression param)
    {
        // sin(x)/x → 1
        if (numerator is MethodCallExpression { Method.Name: "Sin" } sin &&
            sin.Arguments[0] == param &&
            denominator == param)
        {
            return Expression.Constant(1.0);
        }

        // tan(x)/x → 1  
        if (numerator is MethodCallExpression { Method.Name: "Tan" } tan &&
            tan.Arguments[0] == param &&
            denominator == param)
        {
            return Expression.Constant(1.0);
        }

        // (1-cos(x))/x² → 0.5
        if (numerator is BinaryExpression { NodeType: ExpressionType.Subtract } sub &&
            sub.Left.ToString() == "1" &&
            sub.Right is MethodCallExpression { Method.Name: "Cos" } cos &&
            cos.Arguments[0] == param &&
            denominator is BinaryExpression { NodeType: ExpressionType.Multiply } mul &&
            mul.Left == param &&
            mul.Right == param)
        {
            return Expression.Constant(0.5);
        }

        return null;
    }
}