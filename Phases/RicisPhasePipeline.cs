// RicisPhasePipeline.cs

using System.Linq.Expressions;
using SimplifierConsole.Simplifiers;

namespace SimplifierConsole.Phases;

/// <summary>
/// Оркестратор фаз упрощения по RICIS 7.3_safety_patched
/// Строго следует порядку фаз от -1 до 6
/// </summary>
public static class RicisPhasePipeline
{
    public static Expression Simplify(Expression originalExpr)
    {
        var expr = originalExpr;

        // Phase -1: L1_IDENTITY (самоидентичность)
        // Применяется через ExpressionIdentityComparer в последующих фазах

        // Phase 0: Remove limits — прямое вычисление в точке (уже внутри текущей логики)

        //Phase 1: SAFETY CHECK SP2 — Clean First
        // Критично: алгебраическое сокращение ДО RICIS
        expr = AlgebraicSimplifier.CleanFirst(expr);

        // Phase 2: RICIS transforms (∞_F, 0_F/0_G → ∞_{F/G})
        expr = Simplifiers.RicisTransformPhase.Apply(expr);

        //// Phase 3: Algebraic simplification + A6 Bypass (после RICIS)
        expr = AlgebraicSimplifier.ApplyPostRicis(expr);

        //// Phase 4: Type Consistency Protocol (SP3)
        expr = TypeConsistencyPhase.Apply(expr);

        //// Phase 5: Standard operations — уже внутри рекурсивного визитора

        //// Phase 6: L1 verification
        expr = StandardOperationsPhase.Apply(expr);





        return expr;
    }
}