using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using PolynomialProcessing;
using SimplifierConsole;

public static class SingularitySolver
{
    public static List<(ParameterExpression, double)> SolveRoot(Expression expr)
    {
        var roots = new List<(ParameterExpression, double)>();

        if (expr == null) return roots;

        // 1. Specialized solvers first
        // Trigonometric
        var trig = TrigSolver.Solve(expr);
        if (trig.HasValue)
        {
            var t = trig.Value;
            var p = t.Item1;
            var v = t.Item2;
            roots.Add((p, NormalizeZero(v)));
            return roots;
        }

        // Logarithmic
        var log = LogSolver.Solve(expr);
        if (log.HasValue)
        {
            var t = log.Value;
            var p = t.Item1;
            var v = t.Item2;
            roots.Add((p, NormalizeZero(v)));
            return roots;
        }

        // Exponential-like (if present)
        var exp = ExponentialZeroSolver.Solve(expr);
        if (exp.HasValue)
        {
            var t = exp.Value;
            var p = t.Item1;
            var v = t.Item2;
            roots.Add((p, NormalizeZero(v)));
            return roots;
        }

        // 2. Polynomial solver (quadratic)
        var poly = PolynomialParser.ParseQuadratic(expr);
        if (poly.HasValue)
        {
            var (param, a, b, c) = poly.Value;
            if (Math.Abs(a) < double.Epsilon)
            {
                // linear bx + c = 0 -> x = -c/b ; here visitor returns A=0,B=b,C=c
                if (Math.Abs(b) > double.Epsilon)
                {
                    roots.Add((param, NormalizeZero(-c / b)));
                }
            }
            else
            {
                var D = b * b - 4 * a * c;
                if (D >= 0)
                {
                    var sqrtD = Math.Sqrt(D);
                    var x1 = (-b + sqrtD) / (2 * a);
                    var x2 = (-b - sqrtD) / (2 * a);
                    roots.Add((param, NormalizeZero(x1)));
                    if (Math.Abs(x1 - x2) > double.Epsilon) roots.Add((param, NormalizeZero(x2)));
                }
            }
        }

        return roots;
    }

    private static double NormalizeZero(double v) => v == 0.0 ? 0.0 : v;
}