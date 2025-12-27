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

        // 1. Ищем корни знаменателя
        var roots = SingularitySolver.SolveRoot(denominator);
        var foundSingularities = new List<InfinityExpression>();

        if (roots.Count == 0) return b;

        foreach (var root in roots)
        {
            var (paramExpr, rootValue) = root;
            var numValueAtRoot = EvaluateAtPoint(numerator, paramExpr.Name, rootValue);

            // СЛУЧАЙ А: 0 / 0 -> Устранимая сингулярность
            if (Math.Abs(numValueAtRoot) < 1e-10)
            {
                var simplified = PolynomialDivider.TryDivide(numerator, denominator);
                if (simplified != null) return new BridgedExpression(simplified, paramExpr, rootValue);
            }
            // СЛУЧАЙ Б: N / 0 -> Бесконечность
            else
            {
                foundSingularities.Add(new InfinityExpression(numerator, paramExpr, rootValue));
            }
        }

        if (foundSingularities.Count == 1) return foundSingularities[0];
        if (foundSingularities.Count > 1) return new SingularityMonolithExpression(foundSingularities);

        return b;
    }

    private static double EvaluateAtPoint(Expression expr, string paramName, double value)
    {
        var visitor = new SubstitutionVisitor(paramName, value);
        var newExpr = visitor.Visit(expr);
        var lambda = Expression.Lambda<Func<double>>(Expression.Convert(newExpr, typeof(double)));
        return lambda.Compile()();
    }
}

// --- РЕШАТЕЛЬ (SOLVER) ---
public static class SingularitySolver
{
    public static List<(ParameterExpression, double)> SolveRoot(Expression expr)
    {
        var roots = new List<(ParameterExpression, double)>();

        // 1. Пробуем Полиномы
        var poly = PolynomialParser.ParseQuadratic(expr);
        if (poly.HasValue)
        {
            var (param, a, b, c) = poly.Value;
            if (Math.Abs(a) < 1e-10) // Линейное
            {
                if (Math.Abs(b) > 1e-10) roots.Add((param, -c / b));
            }
            else // Квадратное
            {
                var D = b * b - 4 * a * c;
                if (D >= 0)
                {
                    var sqrtD = Math.Sqrt(D);
                    var x1 = (-b + sqrtD) / (2 * a);
                    var x2 = (-b - sqrtD) / (2 * a);
                    roots.Add((param, x1));
                    if (Math.Abs(x1 - x2) > 1e-10) roots.Add((param, x2));
                }
            }
        }

        // 2. Пробуем Тригонометрию (если полином не нашел)
        if (roots.Count == 0)
        {
            var trig = TrigSolver.Solve(expr);
            if (trig.HasValue) roots.Add(trig.Value);
        }

        return roots;
    }
}

// --- ТРИГОНОМЕТРИЯ ---
public static class TrigSolver
{
    public static (ParameterExpression, double)? Solve(Expression expr)
    {
        if (expr is MethodCallExpression call && call.Arguments.Count > 0 &&
            call.Arguments[0] is ParameterExpression param)
        {
            if (call.Method.Name == "Cos") return (param, Math.PI / 2.0);
            if (call.Method.Name == "Sin") return (param, 0.0);
            if (call.Method.Name == "Tan") return (param, 0.0);
        }

        return null;
    }
}

// --- ПАРСЕР ПОЛИНОМОВ ---
public static class PolynomialParser
{
    public static (ParameterExpression, double a, double b, double c)? ParseQuadratic(Expression expr)
    {
        var visitor = new CoefficientsVisitor();
        visitor.Visit(expr);
        if (visitor.Variable == null) return null;
        if (visitor.A == 0 && visitor.B == 0 && visitor.C == 0) return null;
        return (visitor.Variable, visitor.A, visitor.B, visitor.C);
    }

    private class CoefficientsVisitor : ExpressionVisitor
    {
        private double _currentSign = 1.0;
        public ParameterExpression Variable { get; private set; }
        public double A { get; private set; }
        public double B { get; private set; }
        public double C { get; private set; }

        public override Expression Visit(Expression node)
        {
            if (node == null) return null;
            if (node is ConstantExpression c)
            {
                C += Convert.ToDouble(c.Value) * _currentSign;
                return node;
            }

            if (node is ParameterExpression p)
            {
                if (Variable == null) Variable = p;
                if (Variable == p) B += 1.0 * _currentSign;
                return node;
            }

            if (node.NodeType == ExpressionType.Multiply)
            {
                var bin = (BinaryExpression)node;
                // ИСПРАВЛЕНО: Используем else if для предотвращения конфликта имен
                if (bin.Left is ParameterExpression pL1 && bin.Right is ParameterExpression pR1)
                {
                    if (Variable == null) Variable = pL1;
                    A += 1.0 * _currentSign;
                    return node;
                }

                if (bin.Left is ConstantExpression cL2 && bin.Right is ParameterExpression pR2)
                {
                    if (Variable == null) Variable = pR2;
                    B += Convert.ToDouble(cL2.Value) * _currentSign;
                    return node;
                }

                if (bin.Left is ParameterExpression pL3 && bin.Right is ConstantExpression cR3)
                {
                    if (Variable == null) Variable = pL3;
                    B += Convert.ToDouble(cR3.Value) * _currentSign;
                    return node;
                }
            }

            if (node.NodeType == ExpressionType.Add || node.NodeType == ExpressionType.Subtract)
            {
                var bin = (BinaryExpression)node;
                Visit(bin.Left);
                var savedSign = _currentSign;
                if (node.NodeType == ExpressionType.Subtract) _currentSign *= -1;
                Visit(bin.Right);
                _currentSign = savedSign;
                return node;
            }

            if (node.NodeType == ExpressionType.Call) return node; // Игнорируем вызовы методов
            return base.Visit(node);
        }
    }
}