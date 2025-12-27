using System.Linq.Expressions;

public sealed class BridgedExpression : Expression
{
    public Expression Content { get; }           // Результат (например, x + 5)
    public ParameterExpression Variable { get; } // x
    public double SingularityValue { get; }      // 5

    public BridgedExpression(Expression content, ParameterExpression variable, double value)
    {
        Content = content;
        Variable = variable;
        SingularityValue = value;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Content.Type;

    // Вывод: (x + 5) [bridged at x = 5]
    public override string ToString()
        => $"{Content} [bridged at {Variable.Name}={SingularityValue}]";
}