// RicisCore/PolarConverter.cs
using System;
using System.Linq.Expressions;

public static class PolarConverter
{
    /// <summary>
    /// Точное полярное представление ∞_F в RICIS-III.
    /// Радиус всегда ∞.
    /// Угол (сектор) определяется строго по правилам RICIS:
    ///   • Если Numerator точно равен 0 в точке сингулярности → форма 0_F / 0_G → ∞_{F/G} (аксиома A4)
    ///   • Если Numerator вычислим и ≠ 0 → направление по знаку: +π/2 (сверху) или 3π/2 (снизу)
    ///   • Если Numerator не вычислим в точке → требуется символический анализ предела
    /// Никаких численных допусков. Только точное равенство 0.0.
    /// </summary>
    public static string ToPolarSector(InfinityExpression inf, int totalSectors = 8, int maxDenominator = 100)
    {
        if (inf == null) throw new ArgumentNullException(nameof(inf));

        double? numeratorValue = EvaluateNumeratorExactly(inf);

        string radius = "r = ∞";

        // Аксиома A4: точная форма 0_F / 0_G
        if (numeratorValue == 0.0)
        {
            return $"{radius}, θ = индекс F/G (0_F / 0_G → ∞_{{F/G}} по A4 RICIS-III)";
        }

        // Невычислимый числитель в точке (NaN, исключение)
        if (!numeratorValue.HasValue)
        {
            return $"{radius}, θ = требует символического предела (Numerator не вычислим точно в точке сингулярности)";
        }

        // Обычная ∞_F с определённым направлением
        double angleRadians = numeratorValue > 0 ? Math.PI / 2.0 : 3 * Math.PI / 2.0;

        var sectors = CircleSectors.FromRadians(angleRadians, maxDenominator);

        string baseInfo = $"{radius}, θ = {sectors}";
        string sectorInfo = sectors.InSectors(totalSectors);
        string signInfo = numeratorValue > 0 ? "положительная ∞ (сверху)" : "отрицательная ∞ (снизу)";

        return $"{baseInfo} → {sectorInfo} ({signInfo})";
    }

    /// <summary>
    /// Полярное представление для монолита — каждая сингулярность отдельно
    /// </summary>
    public static string ToPolarSector(SingularityMonolithExpression monolith, int totalSectors = 8, int maxDenominator = 100)
    {
        if (monolith?.Singularities == null || monolith.Singularities.Count == 0)
            return "Monolith: нет сингулярностей";

        var lines = monolith.Singularities
            .Select((inf, i) => $"[{i + 1}] {inf.Variable.Name} = {inf.SingularityValue:R}: {ToPolarSector(inf, totalSectors, maxDenominator)}")
            .ToArray();

        return $"Monolith — точные полярные сектора RICIS (из {totalSectors}):\n" + string.Join("\n", lines);
    }

    /// <summary>
    /// Строгое вычисление значения Numerator в точке сингулярности.
    /// Возвращает:
    ///   • double значение, если успешно вычислено и конечно
    ///   • точно 0.0 только при строгом равенстве
    ///   • null при любом исключении, NaN или ±∞
    /// </summary>
    private static double? EvaluateNumeratorExactly(InfinityExpression inf)
    {
        try
        {
            var visitor = new SubstitutionVisitor(inf.Variable.Name, inf.SingularityValue);
            var substituted = visitor.Visit(inf.Numerator);

            var lambda = Expression.Lambda<Func<double>>(Expression.Convert(substituted, typeof(double)));
            double value = lambda.Compile()();

            if (double.IsNaN(value) || double.IsInfinity(value))
                return null;

            return value;
        }
        catch
        {
            return null;
        }
    }

}