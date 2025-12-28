using System.Linq.Expressions;

namespace SimplifierConsole;

/// <summary>
///     Представление одного точного корня
/// </summary>
public readonly struct Root : IEquatable<Root>
{
    public ParameterExpression Parameter { get; }
    public Rational? RationalValue { get; } // если корень рациональный
    public double DoubleValue { get; } // всегда есть (для подстановки)

    public Root(ParameterExpression param, Rational value)
    {
        Parameter = param;
        RationalValue = value;
        DoubleValue = value.ToDouble();
    }

    public Root(ParameterExpression param, double value)
    {
        Parameter = param;
        RationalValue = null;
        DoubleValue = value;
    }

    public bool Equals(Root other)
    {
        return Parameter == other.Parameter &&
               Math.Abs(DoubleValue - other.DoubleValue) == 0;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Parameter, DoubleValue);
    }

    public override string ToString()
    {
        return RationalValue.HasValue
            ? $"{Parameter.Name} = {RationalValue.Value}"
            : $"{Parameter.Name} ≈ {DoubleValue:R}";
    }
}