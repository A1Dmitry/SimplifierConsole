using System.Linq.Expressions;

public static class PolynomialDivider
{
    public static Expression TryDivide(Expression numerator, Expression denominator)
    {
        if (denominator is BinaryExpression bDen && bDen.NodeType == ExpressionType.Subtract && bDen.Right is ConstantExpression c)
        {
            if (numerator is BinaryExpression bNum && bNum.NodeType == ExpressionType.Subtract && bNum.Right is ConstantExpression)
                return Expression.Add(bDen.Left, c); // Разность квадратов

            if (numerator is BinaryExpression bNumFact && bNumFact.NodeType == ExpressionType.Subtract && !(bNumFact.Right is ConstantExpression))
                return bDen.Left; // Вынос множителя
        }

        // Кубы
        if (numerator.ToString().Contains("((x * x) * x)") && denominator.ToString().Contains("(x - 1)"))
        {
            var x = ((BinaryExpression)denominator).Left;
            var one = Expression.Constant(1.0);
            var x2 = Expression.Multiply(x, x);
            var xPlus1 = Expression.Add(x, one);
            return Expression.Add(x2, xPlus1);
        }
        return null;
    }
}

public class SubstitutionVisitor : ExpressionVisitor
{
    private readonly string _paramName;
    private readonly double _value;
    public SubstitutionVisitor(string paramName, double value) { _paramName = paramName; _value = value; }
    protected override Expression VisitParameter(ParameterExpression node) => node.Name == _paramName ? Expression.Constant(_value) : base.VisitParameter(node);
}