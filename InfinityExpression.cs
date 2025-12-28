using System.Linq.Expressions;

namespace SimplifierConsole;

public sealed class InfinityExpression : Expression
{
    public InfinityExpression(Expression numerator, ParameterExpression variable, double value)
    {
        Numerator = numerator;
        Variable = variable;
        SingularityValue = value;
    }

    public Expression Numerator { get; }
    public ParameterExpression Variable { get; }
    public double SingularityValue { get; }
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Numerator.Type;

    public override string ToString()
    {
        return $"∞_{{{Numerator}}} при {Variable.Name} = {SingularityValue:R}";
    }
}

// 2. Монолит (Множественные корни)
public sealed class SingularityMonolithExpression : Expression
{
    public SingularityMonolithExpression(List<InfinityExpression> singularities)
    {
        Singularities = singularities;
    }

    public List<InfinityExpression> Singularities { get; }
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Singularities.FirstOrDefault()?.Type ?? typeof(void);

    public override string ToString()
    {
        if (Singularities == null || Singularities.Count == 0)
            return "Monolith { empty }";

        return $"Monolith {{ {string.Join(", ", Singularities)} }}";
    }
}

// 3. Bridged (Устраненный разрыв)
public sealed class BridgedExpression : Expression
{
    public BridgedExpression(Expression content, ParameterExpression variable, double value)
    {
        Content = content;
        Variable = variable;
        SingularityValue = value;
    }

    public Expression Content { get; }
    public ParameterExpression Variable { get; }
    public double SingularityValue { get; }
    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Content.Type;

    public override string ToString()
    {
        return $"{Content} [bridged at {Variable.Name}={SingularityValue}]";
    }
}