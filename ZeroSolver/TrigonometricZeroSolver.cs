using System.Linq.Expressions;
using SimplifierConsole;

public static class TrigonometricZeroSolver
{
    public static List<Root> FindRoots(Expression expr, ParameterExpression param)
    {
        var roots = new List<Root>();

        if (expr is MethodCallExpression call &&
            call.Method.DeclaringType == typeof(Math) &&
            call.Arguments.Count == 1)
        {
            var arg = call.Arguments[0];

            // Извлекаем линейный коэффициент: k*x + b
            var linear = LinearExtractor.Extract(arg, param);
            if (linear == null) return roots;

            var multiplier = linear.Value.multiplier;
            var offset = linear.Value.offset;

            var baseAngle = call.Method.Name switch
            {
                "Sin" => 0.0 + Math.PI, // sin(θ) = 0 ⇒ θ = kπ (главное — π)
                "Cos" => Math.PI / 2, // cos(θ) = 0 ⇒ θ = π/2 + kπ
                "Tan" => Math.PI, // tan(θ) = 0 ⇒ θ = kπ (но tan имеет полюса!)
                _ => double.NaN
            };

            if (double.IsNaN(baseAngle)) return roots;

            // Главное значение (k=0)
            var theta = baseAngle - offset;
            if (Math.Abs(multiplier) > 1e-10)
            {
                var x = theta / multiplier;
                roots.Add(new Root(param, x));
            }
        }

        return roots;
    }
}