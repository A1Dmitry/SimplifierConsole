using System.Linq.Expressions;

namespace SimplifierConsole.ZeroSolver;

/// <summary>
/// Расширение Phase 2 для тригонометрических сингулярностей
/// Использует TrigSolver для поиска главных корней Cos, Sin, Tan = 0
/// Полное соответствие RICIS 7.3 — только главные корни (principal)
/// </summary>
public static class TrigSingularityExtension
{
    public static List<(ParameterExpression Parameter, double Value)> SolveTrigRoots(Expression denominator)
    {
        var roots = new List<(ParameterExpression, double)>();

        // Поддержка sin(x)/cos(x), tan(ax+b) и т.д.
        if (denominator is MethodCallExpression { Method.Name: "Cos" } call)
        {
            var trigRoot = TrigSolver.Solve(call);
            if (trigRoot.HasValue)
            {
                roots.Add(trigRoot.Value);
            }
        }

        // Если знаменатель — деление Sin / Cos (tan)
        if (denominator is BinaryExpression { NodeType: ExpressionType.Divide } div)
        {
            if (div.Right is MethodCallExpression { Method.Name: "Cos" } cosCall)
            {
                var trigRoot = TrigSolver.Solve(cosCall);
                if (trigRoot.HasValue)
                {
                    roots.Add(trigRoot.Value);
                }
            }
        }

        return roots;
    }
}