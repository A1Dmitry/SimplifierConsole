/// <summary>
/// RicisCalculusCore — Recursive Indexed Calculus of Identity and Singularity (RICIS-III)
/// Автор: Dmitry Aleinikov
/// DOI: 10.5281/zenodo.17872755
///
/// Промышленная реализация RICIS v7.7 с кэшированием,
/// поддержкой A6_GENERAL и заделом под многомерность.
/// </summary>

using System.Linq.Expressions;

namespace RicisCalculusCore;

public static class RicisConfig
{
    public const double Epsilon = 1e-18;
    public const double RootTolerance = 1e-11;
}

public enum SingularityState
{
    Standard,
    Zero,
    Infinity
}

public readonly record struct RicisValue
{
    private RicisValue(double? rawValue, SingularityState state, Expression? index = null)
    {
        RawValue = rawValue;
        State = state;
        Index = index;
    }

    public double? RawValue { get; }

    public SingularityState State { get; }

    public Expression? Index { get; }

    public static RicisValue Standard(double v)
    {
        if (double.IsNaN(v) || double.IsInfinity(v))
            throw new ArgumentException("Use Zero/Infinity.");
        return new RicisValue(v, SingularityState.Standard);
    }

    public static RicisValue Zero(Expression index)
    {
        return new RicisValue(null, SingularityState.Zero, index);
    }

    public static RicisValue Infinity(Expression index)
    {
        return new RicisValue(null, SingularityState.Infinity, index);
    }

    public override string ToString()
    {
        return State switch
        {
            SingularityState.Zero => $"0_{Index}",
            SingularityState.Infinity => $"∞_{Index}",
            _ => RawValue?.ToString() ?? "NaN"
        };
    }

    public static implicit operator RicisValue(double v)
    {
        return Standard(v);
    }
}

/// <summary>
///     Рекурсивно заменяет ВСЕ ParameterExpression с заданным именем и типом на целевой параметр.
///     Работает по имени, а не по ссылке — решает проблему разных экземпляров ParameterExpression.
/// </summary>
internal class DeepParameterUnifier(ParameterExpression targetParam) : ExpressionVisitor
{
    protected override Expression VisitParameter(ParameterExpression node)
    {
        // Сравниваем по имени и типу, а не по ссылке
        if (node.Name == targetParam.Name && node.Type == targetParam.Type)
            return targetParam;
        return node; // не base.VisitParameter, т.к. у ParameterExpression нет детей
    }
}

/// <summary>
///     Запись в кэше: хранит и оптимизированное дерево (для индексации), и скомпилированный делегат.
/// </summary>
internal readonly struct CachedEvaluation(Expression optimized, Func<double, double> compiled)
{
    public Expression OptimizedExpression { get; } = optimized;
    public Func<double, double> CompiledFunction { get; } = compiled;
}

public class RicisExpression(Expression<Func<double, double>> lambda)
{
    private readonly Dictionary<double, CachedEvaluation> _cache = new();
    private readonly ParameterExpression _parameter = lambda.Parameters[0];
    private readonly Expression _syntaxTree = lambda.Body;

    public RicisValue Evaluate(double x)
    {
        // Кэширование оптимизированного дерева и скомпилированного делегата по xKey
        if (!_cache.TryGetValue(x, out var cached))
        {
            var optimized = SingularityResolver.Resolve(_syntaxTree, _parameter, x);
            var compiled = Expression.Lambda<Func<double, double>>(optimized, _parameter).Compile();
            cached = new CachedEvaluation(optimized, compiled);
            _cache[x] = cached;
        }

        var result = cached.CompiledFunction(x);

        // Используем cached.OptimizedExpression для формирования индекса сингулярности (SP4)
        if (double.IsNaN(result)) return RicisValue.Infinity(cached.OptimizedExpression);
        if (double.IsInfinity(result)) return RicisValue.Infinity(cached.OptimizedExpression);
        if (Math.Abs(result) < RicisConfig.Epsilon) return RicisValue.Zero(cached.OptimizedExpression);
        return RicisValue.Standard(result);
    }

    public static RicisExpression operator /(RicisExpression a, RicisExpression b)
    {
        if (a._parameter.Name != b._parameter.Name)
            throw new InvalidOperationException("L1 Violation: Parameters must belong to the same axis.");

        // Заменяем ВСЕ параметры b на параметр a по имени (не по ссылке!)
        var unifiedB = new DeepParameterUnifier(a._parameter).Visit(b._syntaxTree);
        // В дереве a._syntaxTree тоже могут быть "чужие" параметры с тем же именем
        var unifiedA = new DeepParameterUnifier(a._parameter).Visit(a._syntaxTree);

        var divideTree = Expression.Divide(unifiedA, unifiedB);
        // Финальная проверка на всякий случай
        var finalTree = new DeepParameterUnifier(a._parameter).Visit(divideTree);

        return new RicisExpression(Expression.Lambda<Func<double, double>>(finalTree, a._parameter));
    }

    public static RicisExpression operator *(RicisExpression a, RicisExpression b)
    {
        if (a._parameter.Name != b._parameter.Name)
            throw new InvalidOperationException("L1 Violation: Parameters must belong to the same axis.");

        var unifiedA = new DeepParameterUnifier(a._parameter).Visit(a._syntaxTree);
        var unifiedB = new DeepParameterUnifier(a._parameter).Visit(b._syntaxTree);
        var mulTree = Expression.Multiply(unifiedA, unifiedB);
        var finalTree = new DeepParameterUnifier(a._parameter).Visit(mulTree);

        return new RicisExpression(Expression.Lambda<Func<double, double>>(finalTree, a._parameter));
    }
}

public static class SingularityResolver
{
    public static Expression Resolve(Expression tree, ParameterExpression param, double xKey)
    {
        return tree switch
        {
            BinaryExpression { NodeType: ExpressionType.Divide } bin => ResolveDivide(bin, param, xKey),
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
    {
        return Expression.MakeBinary(bin.NodeType, Resolve(bin.Left, param, xKey), Resolve(bin.Right, param, xKey));
    }

    private static Expression ResolveUnary(UnaryExpression un, ParameterExpression param, double xKey)
    {
        return Expression.MakeUnary(un.NodeType, Resolve(un.Operand, param, xKey), un.Type);
    }

    private static Expression ResolveMethodCall(MethodCallExpression call, ParameterExpression param, double xKey)
    {
        var resolvedArgs = call.Arguments.Select(arg => Resolve(arg, param, xKey));
        return Expression.Call(call.Method, resolvedArgs);
    }

    private static Expression? TryReducePolynomials(Expression num, Expression den, ParameterExpression param,
        double xKey)
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
        catch (NotSupportedException)
        {
            // Выражение не является полиномом — это штатная ситуация, сингулярность не сокращается
        }

        return null;
    }

    private static bool IsRoot(double[] coeffs, double root)
    {
        var rem = coeffs.Aggregate<double, double>(0, (current, coefficient) => current * root + coefficient);
        return Math.Abs(rem) < RicisConfig.RootTolerance;
    }

    private static double[] HornerDivide(double[] coeffs, double root)
    {
        if (coeffs.Length == 0) return [];
        if (coeffs.Length == 1) return [0.0];

        var result = new double[coeffs.Length - 1];
        result[0] = coeffs[0];
        for (var i = 1; i < coeffs.Length - 1; i++)
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
            MethodCallExpression mx when y is MethodCallExpression my =>
                mx.Method == my.Method && CompareArguments(mx.Arguments, my.Arguments),
            _ => false
        };
    }

    private static bool CompareBinary(BinaryExpression bx, BinaryExpression by)
    {
        var comm = bx.NodeType is ExpressionType.Add or ExpressionType.Multiply;
        if (comm)
            return (AreEqual(bx.Left, by.Left) && AreEqual(bx.Right, by.Right)) ||
                   (AreEqual(bx.Left, by.Right) && AreEqual(bx.Right, by.Left));
        return AreEqual(bx.Left, by.Left) && AreEqual(bx.Right, by.Right);
    }

    private static bool CompareArguments(IReadOnlyList<Expression> args1, IReadOnlyList<Expression> args2)
    {
        if (args1.Count != args2.Count) return false;
        for (var i = 0; i < args1.Count; i++)
            if (!AreEqual(args1[i], args2[i]))
                return false;
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

        var maxDeg = dict.Keys.Max();
        var result = new double[maxDeg + 1];
        for (var d = 0; d <= maxDeg; d++)
            result[maxDeg - d] = dict.GetValueOrDefault(d, 0.0);
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
        dict[degree] = dict.TryGetValue(degree, out var existing) ? existing + coeff : coeff;
    }

    private static (double coeff, int degree) ParseMonomial(Expression expr, ParameterExpression param)
    {
        if (expr is UnaryExpression { NodeType: ExpressionType.Negate } un)
        {
            var (coeff, deg) = ParseMonomial(un.Operand, param);
            return (-coeff, deg);
        }

        if (expr == param) return (1.0, 1);
        if (expr is ConstantExpression constExpr) return (Convert.ToDouble(constExpr.Value), 0);
        if (expr is BinaryExpression { NodeType: ExpressionType.Multiply } mul)
        {
            var coeff = 1.0;
            var power = 0;
            ExtractFactors(mul, param, ref coeff, ref power);
            return (coeff, power);
        }

        throw new NotSupportedException($"Unsupported monomial: {expr}");
    }

    private static void ExtractFactors(Expression expr, ParameterExpression param, ref double coeff, ref int power)
    {
        if (expr is BinaryExpression { NodeType: ExpressionType.Multiply } mul)
        {
            ExtractFactors(mul.Left, param, ref coeff, ref power);
            ExtractFactors(mul.Right, param, ref coeff, ref power);
        }
        else if (expr == param)
        {
            power++;
        }
        else if (expr is ConstantExpression constExpr)
        {
            coeff *= Convert.ToDouble(constExpr.Value);
        }
        else if (expr is UnaryExpression { NodeType: ExpressionType.Negate } un)
        {
            coeff *= -1;
            ExtractFactors(un.Operand, param, ref coeff, ref power);
        }
        else
        {
            throw new NotSupportedException($"Unsupported factor: {expr.NodeType}");
        }
    }

    private static Expression Negate(Expression e)
    {
        return Expression.Multiply(e, Expression.Constant(-1.0));
    }

    public static Expression ToExpression(double[] coeffs, ParameterExpression param)
    {
        Expression? result = null;
        var degree = coeffs.Length - 1;
        for (var i = 0; i < coeffs.Length; i++)
        {
            var coefficient = coeffs[i];
            var p = degree - i;
            if (Math.Abs(coefficient) < RicisConfig.Epsilon && p > 0) continue;
            Expression term = Expression.Constant(coefficient);
            if (p == 1) term = Expression.Multiply(term, param);
            else if (p > 1) term = Expression.Multiply(term, Expression.Power(param, Expression.Constant((double)p)));
            result = result == null ? term : Expression.Add(result, term);
        }

        return result ?? Expression.Constant(0.0);
    }
}