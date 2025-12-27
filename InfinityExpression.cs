using System.Linq.Expressions;

public sealed class InfinityExpression : Expression
{
    public Expression Numerator { get; }
    public ParameterExpression Variable { get; }
    public double SingularityValue { get; }

    public InfinityExpression(Expression numerator, ParameterExpression variable, double value)
    {
        Numerator = numerator;
        Variable = variable;
        SingularityValue = value;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Numerator.Type;

    public override string ToString()
        => $"∞_{{{Numerator}}} при {Variable.Name} = {SingularityValue}";
}