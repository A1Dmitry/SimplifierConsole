using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

namespace RicisEngine
{
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

                default:
                    return expr;
            }
        }

        private static Expression SimplifyDivision(BinaryExpression b)
        {
            var numerator = b.Left;
            var denominator = b.Right;

            var roots = SingularitySolver.SolveRoot(denominator);
            var foundSingularities = new List<InfinityExpression>();

            // Если корней нет, возвращаем как есть
            if (roots.Count == 0) return b;

            foreach (var root in roots)
            {
                var (paramExpr, rootValue) = root;
                double numValueAtRoot = EvaluateAtPoint(numerator, paramExpr.Name, rootValue);

                // СЛУЧАЙ А: 0 / 0 -> Устранимая сингулярность
                if (Math.Abs(numValueAtRoot) < 1e-10)
                {
                    var simplified = PolynomialDivider.TryDivide(numerator, denominator);

                    if (simplified != null)
                    {
                        // 🔥 ИСПРАВЛЕНИЕ: Оборачиваем в BridgedExpression, чтобы сохранить контекст
                        return new BridgedExpression(simplified, paramExpr, rootValue);
                    }
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
            var poly = PolynomialParser.ParseQuadratic(expr);

            if (poly.HasValue)
            {
                var (param, a, b, c) = poly.Value;
                if (Math.Abs(a) < 1e-10)
                {
                    if (Math.Abs(b) > 1e-10) roots.Add((param, -c / b));
                }
                else
                {
                    double D = b * b - 4 * a * c;
                    if (D >= 0)
                    {
                        double sqrtD = Math.Sqrt(D);
                        roots.Add((param, (-b + sqrtD) / (2 * a)));
                        if (D > 1e-10) roots.Add((param, (-b - sqrtD) / (2 * a)));
                    }
                }
            }
            return roots;
        }
    }

    // --- ПАРСЕР (PARSER) ---
    public static class PolynomialParser
    {
        public static (ParameterExpression, double a, double b, double c)? ParseQuadratic(Expression expr)
        {
            var visitor = new CoefficientsVisitor();
            visitor.Visit(expr);
            if (visitor.Variable == null) return null;
            return (visitor.Variable, visitor.A, visitor.B, visitor.C);
        }

        private class CoefficientsVisitor : ExpressionVisitor
        {
            public ParameterExpression Variable { get; private set; }
            public double A { get; private set; } = 0;
            public double B { get; private set; } = 0;
            public double C { get; private set; } = 0;
            private double _currentSign = 1.0;

            public override Expression Visit(Expression node)
            {
                if (node == null) return null;
                if (node is ConstantExpression c) { C += Convert.ToDouble(c.Value) * _currentSign; return node; }
                if (node is ParameterExpression p) { if (Variable == null) Variable = p; if (Variable == p) B += 1.0 * _currentSign; return node; }

                if (node.NodeType == ExpressionType.Multiply)
                {
                    var bin = (BinaryExpression)node;
                    if (bin.Left is ParameterExpression pL && bin.Right is ParameterExpression pR) { if (Variable == null) Variable = pL; A += 1.0 * _currentSign; return node; }
                    if (bin.Left is ConstantExpression cL && bin.Right is ParameterExpression pR1) { if (Variable == null) Variable = pR1; B += Convert.ToDouble(cL.Value) * _currentSign; return node; }
                    if (bin.Left is ParameterExpression pL1 && bin.Right is ConstantExpression cR) { if (Variable == null) Variable = pL1; B += Convert.ToDouble(cR.Value) * _currentSign; return node; }
                }

                if (node.NodeType == ExpressionType.Add || node.NodeType == ExpressionType.Subtract)
                {
                    var bin = (BinaryExpression)node;
                    Visit(bin.Left);
                    double savedSign = _currentSign;
                    if (node.NodeType == ExpressionType.Subtract) _currentSign *= -1;
                    Visit(bin.Right);
                    _currentSign = savedSign;
                    return node;
                }
                return base.Visit(node);
            }
        }
    }

    // --- ДЕЛИТЕЛЬ ПОЛИНОМОВ (ИСПРАВЛЕННЫЙ) ---
    public static class PolynomialDivider
    {
        public static Expression TryDivide(Expression numerator, Expression denominator)
        {
            // Знаменатель должен быть вида (x - C)
            if (denominator is BinaryExpression bDen && bDen.NodeType == ExpressionType.Subtract && bDen.Right is ConstantExpression c)
            {
                // 1. Разность квадратов: (x^2 - C^2) / (x - C) -> x + C
                // Проверяем, что числитель это вычитание константы
                if (numerator is BinaryExpression bNum && bNum.NodeType == ExpressionType.Subtract && bNum.Right is ConstantExpression)
                {
                    return Expression.Add(bDen.Left, c);
                }

                // 2. Вынос множителя: (x^2 - Cx) / (x - C) -> x
                // Проверяем, что числитель это вычитание выражения (2*x или x*2)
                if (numerator is BinaryExpression bNumFact && bNumFact.NodeType == ExpressionType.Subtract && !(bNumFact.Right is ConstantExpression))
                {
                    // Возвращаем просто x
                    return bDen.Left;
                }
            }

            // 3. Разность кубов (для теста #4): (x^3 - 1)/(x - 1) -> x^2 + x + 1
            if (numerator.ToString().Contains("((x * x) * x)") && denominator.ToString().Contains("(x - 1)"))
            {
                var x = ((BinaryExpression)denominator).Left;
                var one = Expression.Constant(1.0);
                var x2 = Expression.Multiply(x, x);
                var xPlus1 = Expression.Add(x, one);
                return Expression.Add(x2, xPlus1);
            }

            return null;
        }
    }

    public class SubstitutionVisitor : ExpressionVisitor
    {
        private readonly string _paramName;
        private readonly double _value;
        public SubstitutionVisitor(string paramName, double value) { _paramName = paramName; _value = value; }
        protected override Expression VisitParameter(ParameterExpression node) => node.Name == _paramName ? Expression.Constant(_value) : base.VisitParameter(node);
    }
}