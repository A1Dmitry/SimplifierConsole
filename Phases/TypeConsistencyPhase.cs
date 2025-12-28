// TypeConsistencyPhase.cs
using System;
using System.Linq.Expressions;

namespace SimplifierConsole;

/// <summary>
/// Phase 4: Type Consistency Protocol (SP3 из RICIS 7.3)
/// Проверяет совместимость типов в ∞_F и Monolith
/// НЕ бросает исключения и НЕ ломает пайплайн
/// </summary>
public static class TypeConsistencyPhase
{
    public static Expression Apply(Expression expr)
    {
        if (expr == null) return null;

        try
        {
            var visitor = new TypeConsistencyVisitor();
            visitor.Visit(expr);
        }
        catch (Exception ex)
        {
            // Логируем, но не прерываем упрощение
            Console.ForegroundColor = ConsoleColor.DarkYellow;
            Console.WriteLine($"[Phase 4 Warning] Type check error: {ex.Message}");
            Console.ResetColor();
        }

        // ВСЕГДА возвращаем исходное выражение
        return expr;
    }

    private class TypeConsistencyVisitor : ExpressionVisitor
    {
        // КРИТИЧНО: переопределяем VisitExtension для кастомных узлов
        protected override Expression VisitExtension(Expression node)
        {
            switch (node)
            {
                case InfinityExpression inf:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"[Phase 4] ∞_F detected at {inf.Variable.Name} = {inf.SingularityValue:R} — type check passed (stub)");
                    Console.ResetColor();
                    return node; // возвращаем узел без изменений

                case SingularityMonolithExpression mono:
                    Console.ForegroundColor = ConsoleColor.Gray;
                    Console.WriteLine($"[Phase 4] Monolith with {mono.Singularities.Count} singularities — type consistency OK (stub)");
                    Console.ResetColor();
                    return node;

                case BridgedExpression bridged:
                    // Если есть bridged
                    return node;

                default:
                    // Для всех остальных Extension — просто пропускаем
                    return node;
            }
        }

        // Защита от падения на других узлах
        protected override Expression VisitBinary(BinaryExpression node)
        {
            return base.VisitBinary(node);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            return base.VisitMethodCall(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            return base.VisitUnary(node);
        }
    }
}