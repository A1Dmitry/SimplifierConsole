using System.Linq.Expressions;

public static class SingularitySolver
{
    public static List<(ParameterExpression, double)> SolveRoot(Expression expr, ParameterExpression param)
    {
        

        // 1. Полиномы (квадратичные и линейные)
        var roots = new List<(ParameterExpression, double)>();

        var poly = PolynomialParser.ParseQuadratic(expr);
        if (poly.HasValue)
        {
            var (p, a, b, c) = poly.Value;

            // Ключевое место для отладки
            Console.WriteLine($"[RICIS DEBUG] Parsed quadratic for '{expr}': " +
                              $"a = {a:R}, b = {b:R}, c = {c:R} (param: {p.Name})");

            if (p == param)
            {
                if (a == 0.0)
                {
                    Console.WriteLine("[RICIS DEBUG] Linear case");
                    if (b != 0.0)
                    {
                        double root = -c / b;
                        Console.WriteLine($"[RICIS DEBUG] Linear root: {root:R}");
                        roots.Add((param, root));
                    }
                }
                else
                {
                    Console.WriteLine("[RICIS DEBUG] Quadratic case");
                    var D = b * b - 4 * a * c;
                    Console.WriteLine($"[RICIS DEBUG] Discriminant D = {D:R}");

                    if (D > 0)
                    {
                        var sqrtD = Math.Sqrt(D);
                        double r1 = (-b + sqrtD) / (2 * a);
                        double r2 = (-b - sqrtD) / (2 * a);
                        Console.WriteLine($"[RICIS DEBUG] Roots: {r1:R}, {r2:R}");
                        roots.Add((param, r1));
                        roots.Add((param, r2));
                    }
                    else if (D == 0.0)
                    {
                        double root = -b / (2 * a);
                        Console.WriteLine($"[RICIS DEBUG] Double root: {root:R}");
                        roots.Add((param, root));
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