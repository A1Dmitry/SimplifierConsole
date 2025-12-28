// RicisTransformPhase.cs (бывший ExpressionSimplifier.cs)

using System.Linq.Expressions;
using SimplifierConsole.ZeroSolver;

namespace SimplifierConsole.Simplifiers;

/// <summary>
/// Phase 2: RICIS transforms по RICIS 7.3
/// Применяется ТОЛЬКО после Phase 1 (Clean First)
/// </summary>
public static class RicisTransformPhase
{
    public static Expression Apply(Expression expr)
    {
        return new RicisTransformVisitor().Visit(expr);
    }

    private class RicisTransformVisitor : ExpressionVisitor
    {
        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Divide)
            {
                return SimplifyDivision(node.Left, node.Right);
            }

            return base.VisitBinary(node);
        }

        private Expression SimplifyDivision(Expression numerator, Expression denominator)
        {
            var singularities = new List<InfinityExpression>();

            // 1. Полиномиальные / линейные корни
            var polyRoots = SingularitySolver.SolveRoot(denominator);

            foreach (var root in polyRoots)
            {
                // Здесь root — это (ParameterExpression Parameter, double Value)
                // Обращаемся по именам: root.Parameter и root.Value
                AddSingularityIfValid(numerator, denominator, root.Item1, root.Item2, singularities);
            }

            // 2. Тригонометрические корни
            var trigRoot = TrigSolver.Solve(denominator);
            if (trigRoot.HasValue)
            {
                // trigRoot.Value — это (ParameterExpression, double)
                // Деструктурируем или обращаемся по Item1/Item2
                var (param, value) = trigRoot.Value;
                AddSingularityIfValid(numerator, denominator, param, value, singularities);
            }

            if (singularities.Count == 0)
                return Expression.Divide(numerator, denominator);

            return singularities.Count == 1
                ? singularities[0]
                : new SingularityMonolithExpression(singularities);
        }

        private void AddSingularityIfValid(
            Expression numerator,
            Expression denominator,
            ParameterExpression param,
            double value,
            List<InfinityExpression> singularities)
        {
            double numAtRoot = EvaluateAtPoint(numerator, param.Name, value);

            InfinityExpression infinity;
            if (numAtRoot == 0.0) // строгое равенство — RICIS 7.3
            {
                var ricisIndex = Expression.Divide(numerator, denominator);
                infinity = new InfinityExpression(ricisIndex, param, value);
            }
            else
            {
                infinity = new InfinityExpression(numerator, param, value);
            }

            singularities.Add(infinity);
        }

        private static double EvaluateAtPoint(Expression expr, string paramName, double value)
        {
            try
            {
                var visitor = new SubstitutionVisitor(paramName, value);
                var substituted = visitor.Visit(expr);
                var lambda = Expression.Lambda<Func<double>>(Expression.Convert(substituted, typeof(double)));
                return lambda.Compile()();
            }
            catch
            {
                return 1.0; // не вычислимо → считаем ≠0 → полюс
            }
        }
    }
}