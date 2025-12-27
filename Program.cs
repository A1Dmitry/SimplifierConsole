using System.Linq.Expressions;
using System.Text;
using RicisCore;

internal class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("--- RICIS Symbolic Engine: Deep Stress Test ---\n");

        var testCases = new Dictionary<string, Expression<Func<double, double>>>
        {
            { "L1: Basic Singularity (10 / (x - 2))", x => 10 / (x - 2) },

            { "L1: Removable Squares ((x^2 - 25)/(x - 5))", x => (x * x - 25) / (x - 5) },

            { "L2: Coefficients (1 / (2x - 6))", x => 1 / (2 * x - 6) },

            { "L3: Quadratic Denom (1 / (x^2 - 4))", x => 1 / (x * x - 4) },

            { "L5: Simple Trig (sin(x)/cos(x))", x => Math.Sin(x) / Math.Cos(x) },

            { "L6: Sinc Function (sin(x) / x)", x => Math.Sin(x) / x },

            { "L7: Composite Trig (tan(2x))", x => Math.Sin(2 * x) / Math.Cos(2 * x) },

            { "L8: Quartic ((x^4 - 1) / (x - 1))", x => (x * x * x * x - 1) / (x - 1) },

            { "L9: Logarithm (1 / ln(x))", x => 1 / Math.Log(x) }
        };

        var counter = 1;
        foreach (var test in testCases)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Test #{counter}: {test.Key}");
            Console.ResetColor();

            try
            {
                // 1. Запуск RICIS
                var result = ExpressionSimplifier.Simplify(test.Value);

                // 2. Проверка на изменения
                var isUnchanged = result.ToString() == test.Value.ToString();

                Console.WriteLine($"Input:  {test.Value}");

                if (isUnchanged)
                {
                    Console.ForegroundColor = ConsoleColor.Yellow;
                    Console.WriteLine($"Result: {result} (NO SIMPLIFICATION)");
                }
                else
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"Result: {result}");

                    // 3. Если это бесконечность, пробуем Полярные координаты
                    if (result is InfinityExpression || result is SingularityMonolithExpression)
                    {
                        // Хак для демонстрации: берем первую сингулярность
                        var inf = result as InfinityExpression;
                        if (result is SingularityMonolithExpression mono) inf = mono.Singularities[0];

                        if (inf != null)
                        {
                            // Пытаемся вычислить числитель для знака
                            var numVal = 1.0;
                            try
                            {
                                // Подставляем точку разрыва в числитель
                                var visitor = new SubstitutionVisitor(inf.Variable.Name, inf.SingularityValue);
                                var evalExpr = visitor.Visit(inf.Numerator);
                                numVal = Expression.Lambda<Func<double>>(Expression.Convert(evalExpr, typeof(double)))
                                    .Compile()();
                            }
                            catch
                            {
                            }

                            // Создаем временную бесконечность с числом для конвертера
                            var tempInf = new InfinityExpression(Expression.Constant(numVal), inf.Variable,
                                inf.SingularityValue);

                            Console.ForegroundColor = ConsoleColor.Magenta;
                            Console.WriteLine($"Polar:  {PolarConverter.ToPolarSector(tempInf)}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Error: {ex.Message}");
            }

            Console.ResetColor();
            Console.WriteLine(new string('-', 50));
            counter++;
        }
    }
}

/// <summary>
///     Представление типового нуля 0_F — ноль с индексом (Expression) и типом индекса (RicisType).
///     Теперь класс обобщённый: TValue задаёт CLR‑тип значения (как <double>, <int> и т.п.).
/// </summary>
public sealed class TypedZeroExpression<TValue> : Expression
{
    public TypedZeroExpression(Expression indexExpression, RicisType indexType)
    {
        IndexExpression = indexExpression;
        IndexType = indexType ?? RicisType.Scalar;
    }

    public Expression IndexExpression { get; }
    public RicisType IndexType { get; }

    public override ExpressionType NodeType => ExpressionType.Extension;
    public override Type Type => typeof(TValue);

    public override string ToString()
    {
        return $"0_{{{IndexExpression}}}:{typeof(TValue).Name}";
    }
}