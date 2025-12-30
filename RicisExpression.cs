using System.Linq.Expressions;

namespace SimplifierConsole;

public abstract class RicisExpression : Expression
{
    public override ExpressionType NodeType => ExpressionType.Extension;
    public abstract override Type Type { get; }

    // Универсальный структурный оператор
    public static bool operator ==(RicisExpression a, RicisExpression b)
        => ExpressionStructuralComparer.AreEqual(a, b);

    public static bool operator !=(RicisExpression a, RicisExpression b)
        => !ExpressionStructuralComparer.AreEqual(a, b);

    public override bool Equals(object obj)
        => obj is RicisExpression other && ExpressionStructuralComparer.AreEqual(this, other);

    public override int GetHashCode()
        => ToString()?.GetHashCode() ?? 0;
}