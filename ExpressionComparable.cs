using System.Linq.Expressions;

namespace SimplifierConsole;

/// <summary>
/// Универсальная дженерик-обёртка для Expression-потомков.
/// Даёт оператор ==, != и строгую структурную идентичность.
/// </summary>
public readonly struct ExpressionComparable<T> : IEquatable<ExpressionComparable<T>>
    where T : Expression
{
    public T Expr { get; }

    public ExpressionComparable(T expr)
    {
        Expr = expr;
    }

    public static implicit operator ExpressionComparable<T>(T expr)
        => new(expr);

    public static bool operator ==(ExpressionComparable<T> a, ExpressionComparable<T> b)
        => ExpressionStructuralComparer.AreEqual(a.Expr, b.Expr);

    public static bool operator !=(ExpressionComparable<T> a, ExpressionComparable<T> b)
        => !ExpressionStructuralComparer.AreEqual(a.Expr, b.Expr);

    public bool Equals(ExpressionComparable<T> other)
        => ExpressionStructuralComparer.AreEqual(Expr, other.Expr);

    public override bool Equals(object obj)
        => obj is ExpressionComparable<T> other && Equals(other);

    public override int GetHashCode()
        => Expr?.ToString()?.GetHashCode() ?? 0;
}