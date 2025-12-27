using System.Linq.Expressions;
using SimplifierConsole;

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
                var newArgs = call.Arguments.Select(a => Visit(a));
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
        var paramExpr = paramVisitor.Parameter;
        var par = paramVisitor.Parameter;
        if (paramExpr == null) return b;

        var roots = SingularitySolver.SolveRoot(denominator, par);

        if (roots.Count == 0) return b;

        var singularities = new List<Expression>();

        foreach (var root in roots)
        {
            var param = root.Item1;
            var rootValue = root.Item2;

            var numValueAtRoot = EvaluateAtPoint(numerator, param.Name, rootValue);

            if (Math.Abs(numValueAtRoot) < 1e-10)
            {
                var simplified = PolynomialLongDivision.TryDivide(numerator, denominator, param);
                if (simplified != null)
                    singularities.Add(new BridgedExpression(simplified, param, rootValue));
                else
                    singularities.Add(new InfinityExpression(numerator, param, rootValue));
            }
            else
            {
                singularities.Add(new InfinityExpression(numerator, param, rootValue));
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
        var visitor = new SubstitutionVisitor(paramName, value);
        var newExpr = visitor.Visit(expr);
        var lambda = Expression.Lambda<Func<double>>(Expression.Convert(newExpr, typeof(double)));
        return lambda.Compile()();
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
