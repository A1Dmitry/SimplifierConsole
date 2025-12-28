using System.Linq.Expressions;

namespace SimplifierConsole.Phases;

public static class RicisTransformPhase
{
    public static Expression TrySimplify(Expression numerator, Expression denominator, ParameterExpression param)
    {
        // (exp(x)-1)/x → 1
        if (numerator is BinaryExpression
            {
                NodeType: ExpressionType.Subtract, 
                Left: MethodCallExpression
                {
                    Method.Name: "Exp"
                } exp
            } sub &&
            exp.Arguments[0] == param &&
            sub.Right.ToString() == "1" &&
            denominator == param)
        {
            return Expression.Constant(1.0);
        }

        return null;
    }
    private static double EvaluateAtPoint(Expression expr, string paramName, double value)
    {
        try
        {
            var visitor = new SubstitutionVisitor(paramName, value);
            var substituted = visitor.Visit(expr);

            var lambda = Expression.Lambda<Func<double>>(Expression.Convert(substituted, typeof(double)));
            var func = lambda.Compile();
            double result = func();

            // Важно: проверяем на NaN и Infinity отдельно
            if (double.IsNaN(result) || double.IsInfinity(result))
                return 1.0; // неопределённо → считаем ≠0 → обычный полюс

            return result;
        }
        catch
        {
            // Любое исключение (Log(0), 1/0 в числителе и т.д.) → ≠0
            return 1.0;
        }
    }
}