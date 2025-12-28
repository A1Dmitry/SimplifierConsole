using System.Linq.Expressions;

namespace SimplifierConsole.Simplifiers;

/// <summary>
/// Предобработка Phase 1: заменяет Math.Pow(x, n) на x * x * ... (n раз)
/// Для поддержки PolynomialLongDivision на выражениях с Pow
/// </summary>
public class PowToMultiplicationVisitor : ExpressionVisitor
{
    private readonly ParameterExpression _param;

    public PowToMultiplicationVisitor(ParameterExpression param)
    {
        _param = param;
    }

    public static Expression ReplacePow(Expression expr, ParameterExpression param)
    {
        return new PowToMultiplicationVisitor(param).Visit(expr);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (node.Method.Name == "Pow" && node.Arguments.Count == 2)
        {
            var baseArg = Visit(node.Arguments[0]);

            if (baseArg is ParameterExpression p && p == _param && node.Arguments[1] is ConstantExpression expArg && expArg.Value is double dExp)
            {
                int exponent = (int)dExp;
                if (exponent == (double)(int)exponent && exponent >= 0 && exponent <= 10) // ограничение для безопасности
                {
                    Expression power = _param;
                    for (int i = 1; i < exponent; i++)
                    {
                        power = Expression.Multiply(power, _param);
                    }
                    return exponent == 0 ? Expression.Constant(1.0) : power;
                }
            }
        }

        return base.VisitMethodCall(node);
    }
}