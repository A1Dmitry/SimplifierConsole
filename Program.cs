using System.Linq.Expressions;
using System.Text;

internal class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("--- RICIS Symbolic Engine: Deep Stress Test ---\n");

        var testCases = new Dictionary<string, Expression<Func<double, double>>>
        {
            // --- БАЗА (Уже работает) ---
            { "L1: Basic Singularity (10 / (x - 2))", x => 10 / (x - 2) },
            { "L1: Removable Squares ((x^2 - 25)/(x - 5))", x => (x * x - 25) / (x - 5) },
            { "L2: Coefficients (1 / (2x - 6))", x => 1 / (2 * x - 6) },
            { "L3: Quadratic Denom (1 / (x^2 - 4))", x => 1 / (x * x - 4) },
            { "L5: Simple Trig (sin(x)/cos(x))", x => Math.Sin(x) / Math.Cos(x) },

            // --- НОВЫЕ СЛОЖНЫЕ ПРИМЕРЫ ---

            // ==========================================
            // УРОВЕНЬ 6: Смешанные типы (Sinc Function)
            // ==========================================
            // ОЖИДАНИЕ: x=0 -> sin(0)/0 -> 0/0. Предел = 1.
            // ТЕКУЩИЙ СТАТУС: Сломается. PolynomialDivider не умеет делить Sin на x.
            // НУЖНО: Правило Лопиталя или разложение в ряд Тейлора.
            { "L6: Sinc Function (sin(x) / x)", x => Math.Sin(x) / x },

            // ==========================================
            // УРОВЕНЬ 7: Сложные аргументы (Composite)
            // ==========================================
            // ОЖИДАНИЕ: cos(2x)=0 -> 2x=PI/2 -> x=PI/4 (0.785...).
            // ТЕКУЩИЙ СТАТУС: Сломается. TrigSolver смотрит только на чистый 'x'.
            // НУЖНО: Рекурсивный решатель аргументов (Solver Chain).
            { "L7: Composite Trig (tan(2x))", x => Math.Sin(2 * x) / Math.Cos(2 * x) },

            // ==========================================
            // УРОВЕНЬ 8: Высшие степени (High Order)
            // ==========================================
            // ОЖИДАНИЕ: 0/0 -> x^3 + x^2 + x + 1.
            // ТЕКУЩИЙ СТАТУС: Сломается. PolynomialDivider знает только квадраты и хак для куба.
            // НУЖНО: Алгоритм деления полиномов столбиком (Long Division).
            { "L8: Quartic ((x^4 - 1) / (x - 1))", x => (x * x * x * x - 1) / (x - 1) },

            // ==========================================
            // УРОВЕНЬ 9: Логарифмическая сингулярность
            // ==========================================
            // ОЖИДАНИЕ: ln(x) при x=0 -> -Infinity.
            // ТЕКУЩИЙ СТАТУС: Сломается. Нет LogSolver.
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
                            Console.WriteLine($"Polar:  {PolarConverter.ToPolar(tempInf)}");
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