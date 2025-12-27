using System.Linq.Expressions;

public static class SingularitySolver
{
    public static List<(ParameterExpression, double)> SolveRoot(Expression expr, ParameterExpression param)
    {
        var roots = new List<(ParameterExpression, double)>();

        // 1. Полиномы (квадратичные и линейные)
        var poly = PolynomialParser.ParseQuadratic(expr);
        if (poly.HasValue)
        {
            var (p, a, b, c) = poly.Value;
            if (p == param)
            {
                if (Math.Abs(a) < 1e-10) // линейное: bx + c = 0
                {
                    if (Math.Abs(b) > 1e-10)
                        roots.Add((param, -c / b));
                }
                else // квадратичное
                {
                    var D = b * b - 4 * a * c;
                    if (D >= 0)
                    {
                        var sqrtD = Math.Sqrt(D);
                        var r1 = (-b + sqrtD) / (2 * a);
                        var r2 = (-b - sqrtD) / (2 * a);
                        roots.Add((param, r1));
                        if (Math.Abs(r1 - r2) > 1e-12)
                            roots.Add((param, r2));
                    }
                }
            }
        }

        // 2. Тригонометрия — передаём известный параметр
        var trig = TrigSolver.Solve(expr, param);
        if (trig.HasValue)
            roots.Add(trig.Value);

        // 3. Логарифм: ln(x) = 0 ⇒ x = 1
        if (expr is MethodCallExpression logCall &&
            logCall.Method.Name == "Log" &&
            logCall.Arguments.Count == 1 &&
            logCall.Arguments[0] == param)
        {
            roots.Add((param, 1.0));
        }

        return roots;
    }
}