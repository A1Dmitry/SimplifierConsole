using System.Linq.Expressions;
using System.Reflection;

namespace RicisCalculusCore
{
    public static class SingularityResolverTrigonometricExtensions
    {
        private static readonly MethodInfo? _sinMethod = typeof(Math).GetMethod("Sin", [typeof(double)]);
        private static readonly MethodInfo? _cosMethod = typeof(Math).GetMethod("Cos", [typeof(double)]);
        private static readonly MethodInfo? _tanMethod = typeof(Math).GetMethod("Tan", [typeof(double)]);

        public static Expression? TryTrigonometricReduction(
            Expression num, Expression den, ParameterExpression param, double xKey, int depth = 0)
        {
            if (depth >= RicisConfig.MaxTrigReductionDepth) return null;
            if (!IsZeroAtPoint(num, xKey) || !IsZeroAtPoint(den, xKey)) return null;
            if (!ContainsTrigFunctions(num) && !ContainsTrigFunctions(den)) return null;

            var dNum = DerivativeOf(num, param);
            var dDen = DerivativeOf(den, param);
            if (dNum == null || dDen == null) return null;

            var newDivide = Expression.Divide(dNum, dDen);
            var evaluated = TryEvaluateConstant(newDivide);
            if (evaluated != null && !double.IsNaN(evaluated.Value) && !double.IsInfinity(evaluated.Value))
                return Expression.Constant(evaluated.Value);

            return TryTrigonometricReduction(dNum, dDen, param, xKey, depth + 1) ?? newDivide;
        }

        public static bool IsZeroAtPoint(Expression expr, double x)
        {
            try
            {
                // Если выражение — константа, просто проверяем значение
                if (expr is ConstantExpression c)
                    return Math.Abs(Convert.ToDouble(c.Value)) < RicisConfig.Epsilon;

                // Пытаемся скомпилировать с параметром из самого выражения
                var compiled = TryCompileExpression(expr);
                if (compiled != null)
                    return Math.Abs(compiled(x)) < RicisConfig.Epsilon;

                return false;
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Пытается скомпилировать выражение как Func&lt;double, double&gt;,
        /// используя первый найденный параметр или создавая новый.
        /// </summary>
        private static Func<double, double> TryCompileExpression(Expression expr)
        {
            // Извлекаем все параметры из выражения
            var extractor = new ParameterExtractor();
            extractor.Visit(expr);

            if (extractor.FoundParameters.Count == 0)
            {
                // Нет параметров — константное выражение
                var constLambda = Expression.Lambda<Func<double>>(expr);
                var constCompiled = constLambda.Compile();
                double constResult = constCompiled();
                return _ => constResult;
            }

            // Берём первый параметр и создаём лямбду
            var param = extractor.FoundParameters[0];
            var lambda = Expression.Lambda<Func<double, double>>(expr, param);
            return lambda.Compile();
        }

        public static bool ContainsTrigFunctions(Expression expr)
        {
            var visitor = new TrigFunctionFinder();
            visitor.Visit(expr);
            return visitor.Found;
        }

        private sealed class TrigFunctionFinder : ExpressionVisitor
        {
            public bool Found { get; private set; }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                if (node.Method.Name is "Sin" or "Cos" or "Tan")
                    Found = true;
                return base.VisitMethodCall(node);
            }

            protected override Expression VisitExtension(Expression node)
            {
                if (node is ZeroExpression or InfinityExpression)
                    return node;
                return base.VisitExtension(node);
            }
        }

        /// <summary>
        /// Извлекает ВСЕ ParameterExpression из дерева (обходит все типы узлов).
        /// </summary>
        private sealed class ParameterExtractor : ExpressionVisitor
        {
            public List<ParameterExpression> FoundParameters { get; } = [];

            protected override Expression VisitParameter(ParameterExpression node)
            {
                if (!FoundParameters.Any(p => p.Name == node.Name && p.Type == node.Type))
                    FoundParameters.Add(node);
                return base.VisitParameter(node);
            }

            protected override Expression VisitMethodCall(MethodCallExpression node)
            {
                foreach (var arg in node.Arguments)
                    Visit(arg);
                return node;
            }

            protected override Expression VisitBinary(BinaryExpression node)
            {
                Visit(node.Left);
                Visit(node.Right);
                return node;
            }

            protected override Expression VisitUnary(UnaryExpression node)
            {
                Visit(node.Operand);
                return node;
            }

            protected override Expression VisitExtension(Expression node)
            {
                if (node is ZeroExpression zero)
                    Visit(zero.Index);
                else if (node is InfinityExpression inf)
                    Visit(inf.Index);
                return node;
            }

            protected override Expression VisitMember(MemberExpression node)
            {
                if (node.Expression != null)
                    Visit(node.Expression);
                return node;
            }

            protected override Expression VisitConditional(ConditionalExpression node)
            {
                Visit(node.Test);
                Visit(node.IfTrue);
                Visit(node.IfFalse);
                return node;
            }
        }

        public static double? TryEvaluateConstant(Expression expr)
        {
            try
            {
                if (expr is ConstantExpression c)
                    return Convert.ToDouble(c.Value);

                var finder = new ParameterFinder();
                finder.Visit(expr);
                if (finder.HasParameters)
                    return null;

                return Expression.Lambda<Func<double>>(expr).Compile()();
            }
            catch
            {
                return null;
            }
        }

        private sealed class ParameterFinder : ExpressionVisitor
        {
            public bool HasParameters { get; private set; }

            protected override Expression VisitParameter(ParameterExpression node)
            {
                HasParameters = true;
                return base.VisitParameter(node);
            }

            protected override Expression VisitExtension(Expression node)
            {
                if (node is ZeroExpression or InfinityExpression)
                    return node;
                return base.VisitExtension(node);
            }
        }

        public static Expression? DerivativeOf(Expression expr, ParameterExpression param)
        {
            switch (expr)
            {
                case ParameterExpression pe when pe.Name == param.Name:
                    return Expression.Constant(1.0);
                case ConstantExpression:
                    return Expression.Constant(0.0);
                case MethodCallExpression call when call.Method == _sinMethod:
                    return MultiplyOrConstant(Expression.Call(_cosMethod!, call.Arguments[0]), DerivativeOf(call.Arguments[0], param));
                case MethodCallExpression call when call.Method == _cosMethod:
                    return MultiplyOrConstant(Expression.Negate(Expression.Call(_sinMethod!, call.Arguments[0])), DerivativeOf(call.Arguments[0], param));
                case MethodCallExpression call when call.Method == _tanMethod:
                    {
                        var cosU = Expression.Call(_cosMethod!, call.Arguments[0]);
                        var cosSquared = Expression.Multiply(cosU, cosU);
                        var dTan = Expression.Divide(Expression.Constant(1.0), cosSquared);
                        return MultiplyOrConstant(dTan, DerivativeOf(call.Arguments[0], param));
                    }
                // d(u^n)/dx = n * u^(n-1) * du/dx
                case BinaryExpression { NodeType: ExpressionType.Power } powBin:
                    {
                        var baseExpr = powBin.Left;
                        var exponent = powBin.Right;

                        if (exponent is ConstantExpression expConst)
                        {
                            var n = Convert.ToDouble(expConst.Value);
                            var dBase = DerivativeOf(baseExpr, param);
                            if (dBase == null) return null;

                            var newExponent = Expression.Constant(n - 1.0);
                            var powerPart = Expression.Power(baseExpr, newExponent);

                            return MultiplyOrConstant(
                                MultiplyOrConstant(Expression.Constant(n), powerPart),
                                dBase);
                        }
                        return null;
                    }
                case MethodCallExpression call when call.Method.Name == "Pow" && call.Arguments.Count == 2:
                    {
                        var baseExpr = call.Arguments[0];
                        var exponent = call.Arguments[1];

                        if (exponent is ConstantExpression expConst)
                        {
                            var n = Convert.ToDouble(expConst.Value);
                            var dBase = DerivativeOf(baseExpr, param);
                            if (dBase == null) return null;

                            var newExponent = Expression.Constant(n - 1.0);
                            var powerPart = Expression.Call(
                                typeof(Math).GetMethod("Pow", [typeof(double), typeof(double)])!,
                                baseExpr,
                                newExponent);

                            return MultiplyOrConstant(
                                MultiplyOrConstant(Expression.Constant(n), powerPart),
                                dBase);
                        }
                        return null;
                    }
                case BinaryExpression { NodeType: ExpressionType.Add } bin:
                    return SafeBinary(Expression.Add, DerivativeOf(bin.Left, param), DerivativeOf(bin.Right, param));
                case BinaryExpression { NodeType: ExpressionType.Subtract } bin:
                    return SafeBinary(Expression.Subtract, DerivativeOf(bin.Left, param), DerivativeOf(bin.Right, param));
                case BinaryExpression { NodeType: ExpressionType.Multiply } bin:
                    {
                        var dL = DerivativeOf(bin.Left, param);
                        var dR = DerivativeOf(bin.Right, param);
                        if (dL == null || dR == null) return null;
                        return Expression.Add(Expression.Multiply(dL, bin.Right), Expression.Multiply(bin.Left, dR));
                    }
                case BinaryExpression { NodeType: ExpressionType.Divide } bin:
                    {
                        var dL = DerivativeOf(bin.Left, param);
                        var dR = DerivativeOf(bin.Right, param);
                        if (dL == null || dR == null) return null;
                        return Expression.Divide(
                            Expression.Subtract(Expression.Multiply(dL, bin.Right), Expression.Multiply(bin.Left, dR)),
                            Expression.Multiply(bin.Right, bin.Right));
                    }
                case UnaryExpression { NodeType: ExpressionType.Negate } un:
                    {
                        var dOp = DerivativeOf(un.Operand, param);
                        return dOp == null ? null : Expression.Negate(dOp);
                    }
                case ZeroExpression:
                case InfinityExpression:
                    return null;
                default:
                    return null;
            }
        }

        public static Expression? MultiplyOrConstant(Expression a, Expression? b)
        {
            if (b == null) return null;
            if (b is ConstantExpression { Value: 0.0 or 0 }) return Expression.Constant(0.0);
            if (b is ConstantExpression { Value: 1.0 }) return a;
            return Expression.Multiply(a, b);
        }

        public static Expression? SafeBinary(Func<Expression, Expression, Expression> op, Expression? left, Expression? right)
        {
            if (left == null || right == null) return null;
            return op(left, right);
        }
    }
}