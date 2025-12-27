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
    public override string ToString() => $"∞_{{{Numerator}}} при {Variable.Name} = {SingularityValue}";
}

// 2. Монолит (Множественные корни)
public sealed class SingularityMonolithExpression : Expression
{
    public List<InfinityExpression> Singularities { get; }
    public SingularityMonolithExpression(List<InfinityExpression> singularities) { Singularities = singularities; }
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Singularities.FirstOrDefault()?.Type ?? typeof(void);
    public override string ToString() => $"Monolith {{ {string.Join(", ", Singularities)} }}";
}

// 3. Bridged (Устраненный разрыв)
public sealed class BridgedExpression : Expression
{
    public Expression Content { get; }
    public ParameterExpression Variable { get; }
    public double SingularityValue { get; }

    public BridgedExpression(Expression content, ParameterExpression variable, double value)
    {
        Content = content;
        Variable = variable;
        SingularityValue = value;
    }
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Content.Type;
    public override string ToString() => $"{Content} [bridged at {Variable.Name}={SingularityValue}]";
}