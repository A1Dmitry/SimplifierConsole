/// <summary>
/// RicisCalculusCore — Recursive Indexed Calculus of Identity and Singularity (RICIS-III)
/// Автор: Dmitry Aleinikov
/// DOI: 10.5281/zenodo.17872755
///
/// Промышленная реализация согласно RICIS v7.7.
/// </summary>
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace RicisCalculusCore
{
    public static class RicisConfig
    {
        public const double Epsilon = 1e-18;
        public const double RootTolerance = 1e-11;
    }

    public enum SingularityState { Standard, Zero, Infinity }

    public readonly record struct RicisValue
    {
        private readonly double? _rawValue;
        private readonly SingularityState _state;
        private readonly Expression? _index;

        public double? RawValue => _rawValue;
        public SingularityState State => _state;
        public Expression? Index => _index;

        private RicisValue(double? rawValue, SingularityState state, Expression? index = null)
        {
            _rawValue = rawValue; _state = state; _index = index;
        }

        public static RicisValue Standard(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                throw new ArgumentException("Use Zero/Infinity.");
            return new(v, SingularityState.Standard);
        }

        public static RicisValue Zero(Expression index) => new(null, SingularityState.Zero, index);
        public static RicisValue Infinity(Expression index) => new(null, SingularityState.Infinity, index);

        public override string ToString() => _state switch
        {
            SingularityState.Zero => $"0_{_index}",
            SingularityState.Infinity => $"∞_{_index}",
            _ => _rawValue?.ToString() ?? "NaN"
        };

        public static implicit operator RicisValue(double v) => Standard(v);
    }

    public class RicisExpression(Expression<Func<double, double>> lambda)
    {
        private readonly Expression _syntaxTree = lambda.Body;
        private readonly ParameterExpression _parameter = lambda.Parameters[0];

        public Expression SyntaxTree => _syntaxTree;
        public ParameterExpression Parameter => _parameter;

        public RicisValue Evaluate(double x)
        {
            Expression optimized = SingularityResolver.Resolve(_syntaxTree, _parameter, x);
            var final = Expression.Lambda<Func<double, double>>(optimized, _parameter);
            double result = final.Compile()(x);

            if (double.IsNaN(result)) return RicisValue.Infinity(optimized);
            if (double.IsInfinity(result)) return RicisValue.Infinity(optimized);
            if (Math.Abs(result) < RicisConfig.Epsilon) return RicisValue.Zero(optimized);
            return RicisValue.Standard(result);
        }

        public static RicisExpression operator /(RicisExpression a, RicisExpression b)
        {
            if (a._parameter.Name != b._parameter.Name)
                throw new InvalidOperationException("L1 Violation");
            return new RicisExpression(Expression.Lambda<Func<double, double>>(
                Expression.Divide(a._syntaxTree, b._syntaxTree), a._parameter));
        }
    }

    public static class SingularityResolver
    {
        public static Expression Resolve(Expression tree, ParameterExpression param, double xKey)
        {
            return tree switch
            {
                BinaryExpression bin when bin.NodeType == ExpressionType.Divide => ResolveDivide(bin, param, xKey),
                BinaryExpression bin => ResolveBinary(bin, param, xKey),
                UnaryExpression un => ResolveUnary(un, param, xKey),
                MethodCallExpression meth => ResolveMethodCall(meth, param, xKey),
                _ => tree
            };
        }

        private static Expression ResolveDivide(BinaryExpression divide, ParameterExpression param, double xKey)
        {
            var num = Resolve(divide.Left, param, xKey);
            var den = Resolve(divide.Right, param, xKey);
            if (RicisExpressionComparer.AreEqual(num, den)) return Expression.Constant(1.0);

            var reduced = TryReducePolynomials(num, den, param, xKey);
            return reduced != null ? Resolve(reduced, param, xKey) : Expression.Divide(num, den);
        }

        private static Expression ResolveBinary(BinaryExpression bin, ParameterExpression param, double xKey)
            => Expression.MakeBinary(bin.NodeType, Resolve(bin.Left, param, xKey), Resolve(bin.Right, param, xKey));

        private static Expression ResolveUnary(UnaryExpression un, ParameterExpression param, double xKey)
            => Expression.MakeUnary(un.NodeType, Resolve(un.Operand, param, xKey));

        private static Expression ResolveMethodCall(MethodCallExpression call, ParameterExpression param, double xKey)
        {
            var resolvedArgs = call.Arguments.Select(arg => Resolve(arg, param, xKey));
            return Expression.Call(call.Method, resolvedArgs);
        }

        private static Expression? TryReducePolynomials(Expression num, Expression den, ParameterExpression param, double xKey)
        {
            try
            {
                var numCoeffs = PolynomialParser.GetCoefficients(num, param);
                var denCoeffs = PolynomialParser.GetCoefficients(den, param);
                if (IsRoot(numCoeffs, xKey) && IsRoot(denCoeffs, xKey))
                {
                    var newNum = HornerDivide(numCoeffs, xKey);
                    var newDen = HornerDivide(denCoeffs, xKey);
                    return Expression.Divide(
                        PolynomialParser.ToExpression(newNum, param),
                        PolynomialParser.ToExpression(newDen, param));
                }
            }
            catch { }
            return null;
        }

        private static bool IsRoot(double[] coeffs, double root)
        {
            double rem = 0;
            foreach (var c in coeffs) rem = rem * root + c;
            return Math.Abs(rem) < RicisConfig.RootTolerance;
        }

        private static double[] HornerDivide(double[] coeffs, double root)
        {
            if (coeffs.Length == 0) return [];
            if (coeffs.Length == 1) return [0.0]; // деление константы на (x-root) → 0

            double[] result = new double[coeffs.Length - 1];
            result[0] = coeffs[0];
            for (int i = 1; i < coeffs.Length - 1; i++)  // до n-1
                result[i] = coeffs[i] + result[i - 1] * root;
            return result;
        }
    }

    public static class RicisExpressionComparer
    {
        public static bool AreEqual(Expression? x, Expression? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            if (x.NodeType != y.NodeType || x.Type != y.Type) return false;
            return x switch
            {
                BinaryExpression bx when y is BinaryExpression by => CompareBinary(bx, by),
                UnaryExpression ux when y is UnaryExpression uy => AreEqual(ux.Operand, uy.Operand),
                ConstantExpression cx when y is ConstantExpression cy => Equals(cx.Value, cy.Value),
                ParameterExpression px when y is ParameterExpression py => px.Name == py.Name,
                MethodCallExpression mx when y is MethodCallExpression my => mx.Method == my.Method && CompareArguments(mx.Arguments, my.Arguments),
                _ => false
            };
        }

        private static bool CompareBinary(BinaryExpression bx, BinaryExpression by)
        {
            bool comm = bx.NodeType is ExpressionType.Add or ExpressionType.Multiply;
            if (comm)
                return (AreEqual(bx.Left, by.Left) && AreEqual(bx.Right, by.Right)) ||
                       (AreEqual(bx.Left, by.Right) && AreEqual(bx.Right, by.Left));
            return AreEqual(bx.Left, by.Left) && AreEqual(bx.Right, by.Right);
        }

        private static bool CompareArguments(IReadOnlyList<Expression> args1, IReadOnlyList<Expression> args2)
        {
            if (args1.Count != args2.Count) return false;
            for (int i = 0; i < args1.Count; i++)
                if (!AreEqual(args1[i], args2[i])) return false;
            return true;
        }
    }

    public static class PolynomialParser
    {
        public static double[] GetCoefficients(Expression expr, ParameterExpression param)
        {
            var dict = new Dictionary<int, double>();
            CollectTerms(expr, param, dict);
            if (dict.Count == 0) return [0.0];

            int maxDeg = dict.Keys.Max();
            double[] result = new double[maxDeg + 1];
            for (int d = 0; d <= maxDeg; d++)
                result[maxDeg - d] = dict.TryGetValue(d, out var c) ? c : 0.0;
            return result;
        }

        private static void CollectTerms(Expression expr, ParameterExpression param, Dictionary<int, double> dict)
        {
            if (expr is BinaryExpression bin)
            {
                if (bin.NodeType == ExpressionType.Add)
                {
                    CollectTerms(bin.Left, param, dict);
                    CollectTerms(bin.Right, param, dict);
                    return;
                }
                if (bin.NodeType == ExpressionType.Subtract)
                {
                    CollectTerms(bin.Left, param, dict);
                    CollectTerms(Negate(bin.Right), param, dict);
                    return;
                }
            }
            var (coeff, degree) = ParseMonomial(expr, param);
            dict[degree] = dict.GetValueOrDefault(degree) + coeff;
        }

        private static (double coeff, int degree) ParseMonomial(Expression expr, ParameterExpression param)
        {
            if (expr == param) return (1.0, 1);
            if (expr is ConstantExpression c) return (Convert.ToDouble(c.Value), 0);
            if (expr is BinaryExpression mul && mul.NodeType == ExpressionType.Multiply)
            {
                double coeff = 1.0; int power = 0;
                ExtractFactors(mul, param, ref coeff, ref power);
                return (coeff, power);
            }
            throw new NotSupportedException($"Unsupported monomial: {expr}");
        }

        private static void ExtractFactors(Expression expr, ParameterExpression param, ref double coeff, ref int power)
        {
            if (expr is BinaryExpression mul && mul.NodeType == ExpressionType.Multiply)
            {
                ExtractFactors(mul.Left, param, ref coeff, ref power);
                ExtractFactors(mul.Right, param, ref coeff, ref power);
            }
            else if (expr == param) power++;
            else if (expr is ConstantExpression c) coeff *= Convert.ToDouble(c.Value);
            else if (expr is UnaryExpression un && un.NodeType == ExpressionType.Negate)
            {
                coeff *= -1;
                ExtractFactors(un.Operand, param, ref coeff, ref power);
            }
            else throw new NotSupportedException($"Unsupported factor: {expr.NodeType}");
        }

        private static Expression Negate(Expression e) => Expression.Multiply(e, Expression.Constant(-1.0));

        public static Expression ToExpression(double[] coeffs, ParameterExpression param)
        {
            Expression? result = null;
            int degree = coeffs.Length - 1;
            for (int i = 0; i < coeffs.Length; i++)
            {
                double c = coeffs[i];
                int p = degree - i;
                if (Math.Abs(c) < RicisConfig.Epsilon && p > 0) continue;
                Expression term = Expression.Constant(c);
                if (p == 1) term = Expression.Multiply(term, param);
                else if (p > 1) term = Expression.Multiply(term, Expression.Power(param, Expression.Constant((double)p)));
                result = result == null ? term : Expression.Add(result, term);
            }
            return result ?? Expression.Constant(0.0);
        }
    }
}
