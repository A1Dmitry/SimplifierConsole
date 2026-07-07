/// <summary>
/// RicisCalculusCore — Recursive Indexed Calculus of Identity and Singularity (RICIS-III)
/// Автор: Dmitry Aleinikov
/// DOI: 10.5281/zenodo.17872755
///
/// Полная промышленная реализация RICIS v7.7.
/// Включает собственные AST-узлы ZeroExpression и InfinityExpression,
/// аксиомы A1, A4, A5, A6_GENERAL, A7, A10, протоколы SP1-SP4.
/// Порядок применения правил: L1 → SP2 → A4 → A5 → A1.
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
        public static int MaxTrigReductionDepth { get; set; } = 3;
    }

    public enum SingularityState { Standard, Zero, Infinity }

    public readonly record struct RicisValue
    {
        public double? RawValue { get; }
        public SingularityState State { get; }
        public Expression? Index { get; }

        private RicisValue(double? rawValue, SingularityState state, Expression? index = null)
        {
            RawValue = rawValue;
            State = state;
            Index = index;
        }

        public static RicisValue Standard(double v)
        {
            if (double.IsNaN(v) || double.IsInfinity(v))
                throw new ArgumentException("Use Zero/Infinity.");
            return new RicisValue(v, SingularityState.Standard);
        }

        public static RicisValue Zero(Expression index) => new(null, SingularityState.Zero, index);
        public static RicisValue Infinity(Expression index) => new(null, SingularityState.Infinity, index);

        public override string ToString() => State switch
        {
            SingularityState.Zero => $"0_{Index}",
            SingularityState.Infinity => $"∞_{Index}",
            _ => RawValue?.ToString() ?? "NaN"
        };

        public static implicit operator RicisValue(double v) => Standard(v);
    }

    // ═══════════════════════════════════════════════════════════════
    // СОБСТВЕННЫЕ AST-УЗЛЫ СИНГУЛЯРНОСТЕЙ (SP4: Index by expression)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Узел типизированного нуля (0_F). Хранит провенанс — выражение-индекс F.
    /// </summary>
    public class ZeroExpression(Expression index) : Expression
    {
        public Expression Index { get; } = index;
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => typeof(double);
        public override bool CanReduce => false;
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
    }

    /// <summary>
    /// Узел индексированной бесконечности (∞_F). Хранит провенанс — выражение-индекс F.
    /// </summary>
    public class InfinityExpression(Expression index) : Expression
    {
        public Expression Index { get; } = index;
        public override ExpressionType NodeType => ExpressionType.Extension;
        public override Type Type => typeof(double);
        public override bool CanReduce => false;
        protected override Expression VisitChildren(ExpressionVisitor visitor) => this;
    }

    // ═══════════════════════════════════════════════════════════════
    // УНИФИКАТОР ПАРАМЕТРОВ
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Заменяет все ParameterExpression с заданным именем и типом на целевой параметр.
    /// Работает по имени, а не по ссылке. Рекурсивно обрабатывает индексы сингулярностей.
    /// </summary>
    public class DeepParameterUnifier(ParameterExpression targetParam) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node)
        {
            if (node.Name == targetParam.Name && node.Type == targetParam.Type)
                return targetParam;
            return node;
        }

        protected override Expression VisitExtension(Expression node)
        {
            return node switch
            {
                ZeroExpression zero => new ZeroExpression(Visit(zero.Index)),
                InfinityExpression inf => new InfinityExpression(Visit(inf.Index)),
                _ => base.VisitExtension(node)
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // КОМПИЛЯТОР СИНГУЛЯРНОСТЕЙ В DOUBLE
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Заменяет ZeroExpression на 0.0, InfinityExpression на +∞ перед компиляцией в double.
    /// </summary>
    internal class SingularityCompiler : ExpressionVisitor
    {
        protected override Expression VisitExtension(Expression node)
        {
            return node switch
            {
                ZeroExpression => Expression.Constant(0.0),
                InfinityExpression => Expression.Constant(double.PositiveInfinity),
                _ => base.VisitExtension(node)
            };
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // КЭШ ВЫЧИСЛЕНИЙ
    // ═══════════════════════════════════════════════════════════════

    internal readonly struct CachedEvaluation(Expression optimized, Func<double, double> compiled)
    {
        public Expression OptimizedExpression { get; } = optimized;
        public Func<double, double> CompiledFunction { get; } = compiled;
    }

    // ═══════════════════════════════════════════════════════════════
    // ОСНОВНОЙ КЛАСС ВЫРАЖЕНИЯ
    // ═══════════════════════════════════════════════════════════════

    public class RicisExpression(Expression<Func<double, double>> lambda)
    {
        private readonly Expression _syntaxTree = lambda.Body;
        private readonly ParameterExpression _parameter = lambda.Parameters[0];
        private readonly Dictionary<double, CachedEvaluation> _cache = new();

        public RicisValue Evaluate(double x)
        {
            if (!_cache.TryGetValue(x, out var cached))
            {
                // Шаг 1: символьное разрешение сингулярностей (L1, SP2, A1-A10)
                Expression optimized = SingularityResolver.Resolve(_syntaxTree, _parameter, x);

                // Шаг 2: компиляция собственных узлов в double
                var compilable = new SingularityCompiler().Visit(optimized);

                // Шаг 3: компиляция в делегат
                var compiled = Expression.Lambda<Func<double, double>>(compilable, _parameter).Compile();

                cached = new CachedEvaluation(optimized, compiled);
                _cache[x] = cached;
            }

            double result = cached.CompiledFunction(x);

            // Шаг 4: интерпретация результата (SP4: Index by expression)
            if (double.IsNaN(result))
                return RicisValue.Infinity(cached.OptimizedExpression);
            if (double.IsInfinity(result))
                return RicisValue.Infinity(cached.OptimizedExpression);
            if (Math.Abs(result) < RicisConfig.Epsilon)
                return RicisValue.Zero(cached.OptimizedExpression);
            return RicisValue.Standard(result);
        }

        public static RicisExpression operator /(RicisExpression a, RicisExpression b)
        {
            if (a._parameter.Name != b._parameter.Name)
                throw new InvalidOperationException("L1 Violation: Parameters must belong to the same axis.");

            var unifier = new DeepParameterUnifier(a._parameter);
            var unifiedA = unifier.Visit(a._syntaxTree);
            var unifiedB = unifier.Visit(b._syntaxTree);
            var finalTree = unifier.Visit(Expression.Divide(unifiedA, unifiedB));

            return new RicisExpression(Expression.Lambda<Func<double, double>>(finalTree, a._parameter));
        }

        /// <summary>
        /// Умножение с поддержкой A6_GENERAL (0_F × ∞_G = F·G) и A10 (F × 0 = 0_F).
        /// </summary>
        public static RicisExpression operator *(RicisExpression a, RicisExpression b)
        {
            if (a._parameter.Name != b._parameter.Name)
                throw new InvalidOperationException("L1 Violation: Parameters must belong to the same axis.");

            var unifier = new DeepParameterUnifier(a._parameter);
            var unifiedA = unifier.Visit(a._syntaxTree);
            var unifiedB = unifier.Visit(b._syntaxTree);
            var finalTree = unifier.Visit(Expression.Multiply(unifiedA, unifiedB));

            return new RicisExpression(Expression.Lambda<Func<double, double>>(finalTree, a._parameter));
        }

        /// <summary>
        /// Вычитание с поддержкой A7 (∞_F − ∞_G = ∞_{F−G}).
        /// </summary>
        public static RicisExpression operator -(RicisExpression a, RicisExpression b)
        {
            if (a._parameter.Name != b._parameter.Name)
                throw new InvalidOperationException("L1 Violation: Parameters must belong to the same axis.");

            var unifier = new DeepParameterUnifier(a._parameter);
            var unifiedA = unifier.Visit(a._syntaxTree);
            var unifiedB = unifier.Visit(b._syntaxTree);
            var finalTree = unifier.Visit(Expression.Subtract(unifiedA, unifiedB));

            return new RicisExpression(Expression.Lambda<Func<double, double>>(finalTree, a._parameter));
        }

        /// <summary>
        /// Сложение выражений.
        /// </summary>
        public static RicisExpression operator +(RicisExpression a, RicisExpression b)
        {
            if (a._parameter.Name != b._parameter.Name)
                throw new InvalidOperationException("L1 Violation: Parameters must belong to the same axis.");

            var unifier = new DeepParameterUnifier(a._parameter);
            var unifiedA = unifier.Visit(a._syntaxTree);
            var unifiedB = unifier.Visit(b._syntaxTree);
            var finalTree = unifier.Visit(Expression.Add(unifiedA, unifiedB));

            return new RicisExpression(Expression.Lambda<Func<double, double>>(finalTree, a._parameter));
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // РЕЗОЛВЕР СИНГУЛЯРНОСТЕЙ
    // Порядок применения правил: L1 → SP2 → A4 → A5 → A1
    // Аксиомы: A1, A4, A5, A6_GENERAL, A7, A10
    // ═══════════════════════════════════════════════════════════════

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
                ZeroExpression zero => ResolveZero(zero, param, xKey),
                InfinityExpression inf => ResolveInfinity(inf, param, xKey),
                _ => tree
            };
        }

        // ─── Деление ───────────────────────────────────────────
        // Порядок: L1 → SP2 → A4 → A5 → A1

        private static Expression ResolveDivide(BinaryExpression divide, ParameterExpression param, double xKey)
        {
            var num = Resolve(divide.Left, param, xKey);
            var den = Resolve(divide.Right, param, xKey);

            // L1 (SP1): X / X → 1 — структурное тождество, наивысший приоритет
            if (RicisExpressionComparer.AreEqual(num, den))
                return Expression.Constant(1.0);

            // SP2: полиномиальное сокращение — очистка до аксиом
            var reduced = TryReducePolynomials(num, den, param, xKey);
            if (reduced != null)
                return Resolve(reduced, param, xKey);
            // A11: раскрытие тригонометрических сингулярностей
            var trigReduced = SingularityResolverTrigonometricExtensions.TryTrigonometricReduction(num, den, param, xKey);
            if (trigReduced != null)
                return Resolve(trigReduced, param, xKey);
            // A4: 0_F / 0_G → F/G (отношение индексов)
            if (num is ZeroExpression zNum && den is ZeroExpression zDen)
                return Resolve(Expression.Divide(zNum.Index, zDen.Index), param, xKey);

            // A5: ∞_F / ∞_G → F/G
            if (num is InfinityExpression iNum && den is InfinityExpression iDen)
                return Resolve(Expression.Divide(iNum.Index, iDen.Index), param, xKey);

            // A1: F / 0 → ∞_F — применяется последним
            if (den is ConstantExpression { Value: 0.0 or 0 })
                return new InfinityExpression(num);

            // Ни одно правило не сработало — оставляем как есть
            return Expression.Divide(num, den);
        }

        // ─── Бинарные операции ─────────────────────────────────
        // Аксиомы: A10, A7, A6_GENERAL

        private static Expression ResolveBinary(BinaryExpression bin, ParameterExpression param, double xKey)
        {
            var left = Resolve(bin.Left, param, xKey);
            var right = Resolve(bin.Right, param, xKey);

            // A10: F × 0 = 0_F
            if (bin.NodeType == ExpressionType.Multiply)
            {
                if (right is ConstantExpression { Value: 0.0 or 0 })
                    return new ZeroExpression(left);
                if (left is ConstantExpression { Value: 0.0 or 0 })
                    return new ZeroExpression(right);
            }

            // A7: ∞_F − ∞_G = ∞_{F−G}
            if (bin.NodeType == ExpressionType.Subtract)
            {
                if (left is InfinityExpression infL && right is InfinityExpression infR)
                    return new InfinityExpression(
                        Resolve(Expression.Subtract(infL.Index, infR.Index), param, xKey));
            }

            // A6_GENERAL: 0_F × ∞_G = F·G
            if (bin.NodeType == ExpressionType.Multiply)
            {
                if (left is ZeroExpression z && right is InfinityExpression inf)
                    return Resolve(Expression.Multiply(z.Index, inf.Index), param, xKey);
                if (left is InfinityExpression inf2 && right is ZeroExpression z2)
                    return Resolve(Expression.Multiply(inf2.Index, z2.Index), param, xKey);
            }

            return Expression.MakeBinary(bin.NodeType, left, right);
        }

        // ─── Унарные операции ──────────────────────────────────

        private static Expression ResolveUnary(UnaryExpression un, ParameterExpression param, double xKey)
            => Expression.MakeUnary(un.NodeType, Resolve(un.Operand, param, xKey), un.Type);

        // ─── Вызовы методов ────────────────────────────────────

        private static Expression ResolveMethodCall(MethodCallExpression call, ParameterExpression param, double xKey)
        {
            var resolvedArgs = call.Arguments.Select(arg => Resolve(arg, param, xKey));
            return Expression.Call(call.Method, resolvedArgs);
        }

        // ─── Рекурсивная очистка индексов сингулярностей ──────

        private static Expression ResolveZero(ZeroExpression zero, ParameterExpression param, double xKey)
            => new ZeroExpression(Resolve(zero.Index, param, xKey));

        private static Expression ResolveInfinity(InfinityExpression inf, ParameterExpression param, double xKey)
            => new InfinityExpression(Resolve(inf.Index, param, xKey));

        // ─── Полиномиальное сокращение (SP2) ───────────────────

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
            catch (NotSupportedException)
            {
                // Выражение не является полиномом — сингулярность не сокращается
            }
            return null;
        }

        private static bool IsRoot(double[] coeffs, double root)
        {
            double rem = 0;
            foreach (var coefficient in coeffs) rem = rem * root + coefficient;
            return Math.Abs(rem) < RicisConfig.RootTolerance;
        }

        private static double[] HornerDivide(double[] coeffs, double root)
        {
            if (coeffs.Length == 0) return Array.Empty<double>();
            if (coeffs.Length == 1) return new[] { 0.0 };

            double[] result = new double[coeffs.Length - 1];
            result[0] = coeffs[0];
            for (int i = 1; i < coeffs.Length - 1; i++)
                result[i] = coeffs[i] + result[i - 1] * root;
            return result;
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // КОМПАРАТОР ВЫРАЖЕНИЙ (ДЛЯ L1)
    // ═══════════════════════════════════════════════════════════════

    public static class RicisExpressionComparer
    {
        public static bool AreEqual(Expression? x, Expression? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (x == null || y == null) return false;
            if (x.NodeType != y.NodeType || x.Type != y.Type) return false;

            return x switch
            {
                ZeroExpression zx when y is ZeroExpression zy => AreEqual(zx.Index, zy.Index),
                InfinityExpression ix when y is InfinityExpression iy => AreEqual(ix.Index, iy.Index),
                BinaryExpression bx when y is BinaryExpression by => CompareBinary(bx, by),
                UnaryExpression ux when y is UnaryExpression uy => AreEqual(ux.Operand, uy.Operand),
                ConstantExpression cx when y is ConstantExpression cy => Equals(cx.Value, cy.Value),

                // L1: параметры сравниваются и по имени, и по типу
                ParameterExpression px when y is ParameterExpression py =>
                    px.Name == py.Name && px.Type == py.Type,

                MethodCallExpression mx when y is MethodCallExpression my =>
                    mx.Method == my.Method && CompareArguments(mx.Arguments, my.Arguments),

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

    // ═══════════════════════════════════════════════════════════════
    // УНИВЕРСАЛЬНЫЙ ПАРСЕР ПОЛИНОМОВ
    // ═══════════════════════════════════════════════════════════════

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
                var coeff = 1.0; var power = 0;
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
            else if (expr == param) power++;
            else if (expr is ConstantExpression constExpr) coeff *= Convert.ToDouble(constExpr.Value);
            else if (expr is UnaryExpression { NodeType: ExpressionType.Negate } un)
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
}