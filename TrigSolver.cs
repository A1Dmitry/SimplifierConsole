using System.Linq.Expressions;

public static class TrigSolver
{
    /// <summary>
    /// Находит простые тригонометрические сингулярности.
    /// Поддерживает: sin(expr)=0, cos(expr)=0, где expr = k*x + b
    /// </summary>
    public static (ParameterExpression param, double rootValue)? Solve(Expression trigExpression, ParameterExpression knownParam)
    {
        if (trigExpression is MethodCallExpression call &&
            call.Method.DeclaringType == typeof(Math) &&
            (call.Method.Name == "Sin" || call.Method.Name == "Cos") &&
            call.Arguments.Count == 1)
        {
            var arg = call.Arguments[0];

            // Пытаемся представить аргумент как линейный: k * param + b
            var linear = LinearExtractor.Extract(arg, knownParam);

            double k = 1.0;
            double b = 0.0;

            if (linear.HasValue)
            {
                k = linear.Value.multiplier;
                b = linear.Value.offset;
            }
            else if (arg.NodeType == ExpressionType.Multiply &&
                     arg is BinaryExpression mul &&
                     mul.Left is ConstantExpression c && mul.Right == knownParam)
            {
                k = Convert.ToDouble(c.Value);
            }
            else if (arg != knownParam)
            {
                // Не линейный и не просто param — пока не поддерживаем
                return null;
            }

            if (Math.Abs(k) < 1e-12) return null; // константа

            if (call.Method.Name == "Cos")
            {
                // cos(kx + b) = 0 ⇒ kx + b = π/2 + π*n ⇒ первое положительное решение
                double root = (Math.PI / 2.0 - b) / k;
                return (knownParam, root);
            }

            if (call.Method.Name == "Sin")
            {
                // sin(kx + b) = 0 ⇒ kx + b = π*n ⇒ первое решение
                double root = (0.0 - b) / k;
                return (knownParam, root);
            }
        }

        // Поддержка прямого tan(x) — tan(x) = sin(x)/cos(x), сингулярность при cos(x)=0
        if (trigExpression is MethodCallExpression tanCall &&
            tanCall.Method.Name == "Tan" &&
            tanCall.Arguments.Count == 1 &&
            tanCall.Arguments[0] == knownParam)
        {
            return (knownParam, Math.PI / 2.0);
        }

        return null;
    }
}