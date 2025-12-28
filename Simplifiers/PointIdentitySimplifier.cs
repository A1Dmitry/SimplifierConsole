using System.Linq.Expressions;

namespace SimplifierConsole.Simplifiers;

public static class PointIdentitySimplifier
{
    public static Expression TrySimplify(Expression numerator, Expression denominator,
        ParameterExpression param, double point)
    {
        if (point != 0.0) return null;

        // ТОЛЬКО 3 доказанных тождества
        if (IsMatch(numerator, denominator, param, "Sin")) return One;
        if (IsMatch(numerator, denominator, param, "Tan")) return One;
        if (IsExpMinus1Match(numerator, denominator, param)) return One;
        

        return null;
    }

    private static readonly Expression One = Expression.Constant(1.0);

    private static bool IsMatch(Expression num, Expression den,
        ParameterExpression param, string funcName)
    {
        return num is MethodCallExpression call &&
               call.Method.Name == funcName &&
               call.Arguments[0] == param &&
               den == param;
    }

    private static bool IsExpMinus1Match(Expression num, Expression den,
        ParameterExpression param)
    {
        return num is BinaryExpression
               {
                   NodeType: ExpressionType.Subtract, 
                   Left: MethodCallExpression
                   {
                       Method.Name: "Exp"
                   } exp
               } sub &&
               exp.Arguments[0] == param &&
               sub.Right.ToString() == "1" &&
               den == param;
    }


}