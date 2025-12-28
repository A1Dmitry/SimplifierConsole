// ExpressionSimplifier.cs (обновлённая версия с полным закрытием разрывов)

using SimplifierConsole;
using SimplifierConsole.Simplifiers;
using System.Linq.Expressions;

public static class ExpressionSimplifier
{
    public static Expression Simplify(Expression expr)
    {
        return Visit(expr);
    }

    private static Expression Visit(Expression expr)
    {
        if (expr == null) return null;

        switch (expr.NodeType)
        {
            case ExpressionType.Divide:
                return SimplifyDivision((BinaryExpression)expr);

            case ExpressionType.Add:
            case ExpressionType.Subtract:
            case ExpressionType.Multiply:
                return Expression.MakeBinary(expr.NodeType,
                    Visit(((BinaryExpression)expr).Left),
                    Visit(((BinaryExpression)expr).Right));

            case ExpressionType.Lambda:
                var lambda = (LambdaExpression)expr;
                return Expression.Lambda(Visit(lambda.Body), lambda.Parameters);

            case ExpressionType.Call:
                var call = (MethodCallExpression)expr;
                var newArgs = call.Arguments.Select(Visit);
                var newObj = Visit(call.Object);
                return Expression.Call(newObj, call.Method, newArgs);

            default:
                return expr;
        }
    }

    private static Expression SimplifyDivision(BinaryExpression b)
    {
        var numerator = b.Left;
        var denominator = b.Right;

        var paramVisitor = new ParameterFinderVisitor();
        paramVisitor.Visit(b);
        var param = paramVisitor.Parameter;
        if (param == null) return b;

        var roots = SingularitySolver.SolveRoot(denominator, param);

        if (roots.Count == 0) return b;

        var singularities = new List<Expression>();

        foreach (var root in roots)
        {
            var rootParam = root.Item1;
            var rootValue = root.Item2;

            var numValueAtRoot = EvaluateAtPoint(numerator, rootParam.Name, rootValue);

            if (numValueAtRoot == 0.0) // точная форма 0_F / 0_G — разрыв
            {
                Console.WriteLine($"[DEBUG] Found 0/0 at x={rootValue}");
                Console.WriteLine($"[DEBUG] Numerator: {numerator}");
                Console.WriteLine($"[DEBUG] Denominator: {denominator}");
                // Классика закрывает только полиномиальные разрывы
                var simplified = PolynomialLongDivision.TryDivide(numerator, denominator, rootParam);
                Console.WriteLine($"[DEBUG] PolynomialLongDivision result: {simplified?.ToString() ?? "null"}");
                simplified ??= PointIdentitySimplifier.TrySimplify(numerator, denominator, rootParam, rootValue);
                if (simplified != null)
                {
                    Console.WriteLine($"[DEBUG] Simplified type: {simplified.GetType().Name}");
                    Console.WriteLine($"[DEBUG] Simplified value: {simplified}");
                    // bridged — классика справилась
                    singularities.Add(new BridgedExpression(simplified, rootParam, rootValue));
                }
                else
                {
                    // RICIS закрывает все остальные разрывы по A4
                    var ricisIndex = Expression.Divide(numerator, denominator); // F/G
                    singularities.Add(new InfinityExpression(ricisIndex, rootParam, rootValue));
                }
            }
            else
            {
                
                // Обычный полюс
                singularities.Add(new InfinityExpression(numerator, rootParam, rootValue));
            }
        }

        return singularities.Count switch
        {
            0 => b,
            1 => singularities[0],
            _ => new SingularityMonolithExpression(singularities.OfType<InfinityExpression>().ToList())
        };
    }

    private static double EvaluateAtPoint(Expression expr, string paramName, double value)
    {
        try
        {
            var visitor = new SubstitutionVisitor(paramName, value);
            var newExpr = visitor.Visit(expr);
            var lambda = Expression.Lambda<Func<double>>(Expression.Convert(newExpr, typeof(double)));
            return lambda.Compile()();
        }
        catch
        {
            // Если не вычислимо (Log(0) и т.п.) — считаем не нулём (полюс)
            return 1.0;
        }
    }

    private class ParameterFinderVisitor : ExpressionVisitor
    {
        public ParameterExpression Parameter { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Parameter ??= node;
            return base.VisitParameter(node);
        }
    }
}