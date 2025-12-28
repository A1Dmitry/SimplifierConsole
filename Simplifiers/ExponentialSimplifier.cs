using System.Linq.Expressions;

namespace SimplifierConsole.Simplifiers;

public static class ExponentialSimplifier
{
    public static Expression TrySimplify(Expression numerator, Expression denominator, ParameterExpression param)
    {
        // (exp(x)-1)/x → 1
        if (numerator is BinaryExpression
            {
                NodeType: ExpressionType.Subtract, 
                Left: MethodCallExpression
                {
                    Method.Name: "Exp"
                } exp
            } sub &&
            exp.Arguments[0] == param &&
            sub.Right.ToString() == "1" &&
            denominator == param)
        {
            return Expression.Constant(1.0);
        }

        return null;
    }
}