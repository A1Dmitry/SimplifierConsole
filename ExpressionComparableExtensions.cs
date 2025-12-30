using System.Linq.Expressions;

namespace SimplifierConsole;

public static class ExpressionComparableExtensions
{
    public static ExpressionComparable<T> AsComparable<T>(this T expr)
        where T : Expression
        => new(expr);

}