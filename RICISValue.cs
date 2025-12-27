using System.Linq.Expressions;

public struct RICISValue<T>
{
    public T Value { get; }
    public bool IsSingularity { get; }
    public Expression<Func<RICISValue<T>>> Numerator { get; }

    // Обычное значение
    public RICISValue(T value)
    {
        Value = value;
        IsSingularity = false;
        Numerator = null;
    }

    // Сингулярность
    public RICISValue(Expression<Func<RICISValue<T>>> numerator)
    {
        Value = default;
        IsSingularity = true;
        Numerator = numerator;
    }

    public override string ToString()
    {
        return IsSingularity ? $"0_F({Numerator.Body})" : Value.ToString();
    }

    // Деление
    public static RICISValue<T> operator /(RICISValue<T> a, RICISValue<T> b)
    {
        if (b.IsSingularity)
            return a; // F / 0_F → F
        return new RICISValue<T>((dynamic)a.Value / (dynamic)b.Value);
    }

    // Сложение
    public static RICISValue<T> operator +(RICISValue<T> a, RICISValue<T> b)
    {
        if (a.IsSingularity || b.IsSingularity)
            throw new InvalidOperationException("Нельзя складывать сингулярности напрямую.");
        return new RICISValue<T>((dynamic)a.Value + (dynamic)b.Value);
    }
}