using System.Linq.Expressions;

public class SubstitutionVisitor : ExpressionVisitor
{
    private readonly string _paramName;
    private readonly double _value;
    public SubstitutionVisitor(string paramName, double value) { _paramName = paramName; _value = value; }
    protected override Expression VisitParameter(ParameterExpression node) => node.Name == _paramName ? Expression.Constant(_value) : base.VisitParameter(node);
}
