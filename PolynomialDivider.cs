using System.Linq.Expressions;

namespace SimplifierConsole;

public static class PolynomialDivider
{
    public static Expression TryDivide(Expression numerator, Expression denominator)
    {
        if (denominator is BinaryExpression { NodeType: ExpressionType.Subtract, Right: ConstantExpression c } bDen)
        {
            if (numerator is BinaryExpression { NodeType: ExpressionType.Subtract, Right: ConstantExpression })
                return Expression.Add(bDen.Left, c); // Разность квадратов

            if (numerator is BinaryExpression { NodeType: ExpressionType.Subtract } bNumFact &&
                !(bNumFact.Right is ConstantExpression))
                return bDen.Left; // Вынос множителя
        }

        // Кубы
        

        return null;
    }

   

    private static bool IsX3(Expression expr, ParameterExpression x)
    {
        // Проверяем x * x * x
        if (expr is BinaryExpression { NodeType: ExpressionType.Multiply } m1)
            if (m1.Left is BinaryExpression { NodeType: ExpressionType.Multiply } m2)
                return m2.Left == x && m2.Right == x && m1.Right == x;
        return false;
    }
}

public class SubstitutionVisitor : ExpressionVisitor
{
    private readonly string _paramName;
    private readonly double _value;

    public SubstitutionVisitor(string paramName, double value)
    {
        _paramName = paramName;
        _value = value;
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        return node.Name == _paramName ? Expression.Constant(_value) : base.VisitParameter(node);
    }
}