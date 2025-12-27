using System.Linq.Expressions;

public sealed class SingularityMonolithExpression : Expression
{
    public List<InfinityExpression> Singularities { get; }

    public SingularityMonolithExpression(List<InfinityExpression> singularities)
    {
        Singularities = singularities;
    }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => Singularities.FirstOrDefault()?.Type ?? typeof(void);

    public override string ToString()
    {
        return $"Monolith {{ {string.Join(", ", Singularities)} }}";
    }
}