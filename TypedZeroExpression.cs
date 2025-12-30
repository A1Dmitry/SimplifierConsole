using System.Linq.Expressions;

namespace SimplifierConsole;

/// <summary>
///     Представление типового нуля 0_F — ноль с индексом (Expression) и типом индекса (RicisType).
///     Теперь класс обобщённый: TValue задаёт CLR‑тип значения (как <double>, <int> и т.п.).
/// </summary>
public sealed class TypedZeroExpression<TValue> : RicisExpression
{
    public TypedZeroExpression(Expression indexExpression, RicisType indexType)
    {
        IndexExpression = indexExpression;
        IndexType = indexType ?? RicisType.Scalar;
    }

    public Expression IndexExpression { get; }
    public RicisType IndexType { get; }

    public override Type Type => typeof(TValue);

    public override string ToString()
    {
        return $"0_{{{IndexExpression}}}:{typeof(TValue).Name}";
    }
}