using System.Linq.Expressions;

public static class PolarConverter
{
    public static string ToPolar(Expression expr)
    {
        if (expr is InfinityExpression inf)
        {
            double val = 1.0;
            if (inf.Numerator is ConstantExpression c) val = Convert.ToDouble(c.Value);

            double theta = val >= 0 ? Math.PI / 2 : -Math.PI / 2;
            return $"Polar(r=∞, θ={theta:F4} rad [{theta * 180 / Math.PI:F0}°])";
        }
        return "Finite";
    }
}