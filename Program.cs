using System.Linq.Expressions;
using System.Text;
using SimplifierConsole.Simplifiers;

internal partial class Program
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

                { "L9: Logarithm (1 / ln(x))", x => 1 / Math.Log(x) },


                // Новый L10: Классический 0/0 — (e^x - 1)/x при x=0
                { "L10: Exponential removable ((exp(x) - 1)/x)", x => (Math.Exp(x) - 1) / x },

                // Новый L11: (1 - cos(x))/x² при x=0 (классика даёт 1/2 через ряд)
                { "L11: Trig identity ((1 - cos(x))/x²)", x => (1 - Math.Cos(x)) / (x * x) },

                // Новый L12: tan(x)/x при x=0 (ещё один трансцендентный 0/0)
                { "L12: Tan(x)/x", x => Math.Tan(x) / x },

                // Новый L13: Navier-Stokes вдохновлённый — упрощённая сингулярность в вязком термине
                // Рассматриваем типичный член вида 1/Re, где Re = ρ u L / μ → при u→0 (застой) может быть сингулярность
                // Простая модель: 1 / (x * (x + 1)) — имитирует поведение вблизи стенки (x ~ расстояние до стенки)
                { "L13: Navier-Stokes wall analogy (1 / (x*(x+1)))", x => 1 / (x * (x + 1)) },

                // Новый L14: Более жёсткий — потенциальная blow-up в нелинейном члене Навье-Стокса
                // Упрощённая модель возможного конечного времени сингулярности: 1/(1 - x²) при x→1
                { "L14: NS blow-up model (1 / (1 - x²))", x => 1 / (1 - x * x) },
                // Новые известные сингулярности
                { "L15: Essential Singularity (exp(1/z) at z=0)", x => Math.Exp(1 / x) },  // Классический essential singularity

                { "L16: Simple Pole (1/z at z=0)", x => 1 / x },

                { "L17: Pole of order 2 (1/z² at z=0)", x => 1 / (x * x) },

                { "L18: Logarithmic Singularity (Log(z) at z=0)", x => Math.Log(x) },  // Branch point, но полюс в мнимой части

                { "L19: Removable Singularity classic (sin(x)/x at x=0)", x => Math.Sin(x) / x },  // Уже был, но с именем

                { "L20: Picard Theorem example (exp(1/z) essential)", x => Math.Exp(1 / x) },  // Повтор для демонстрации

                { "L21: Big Bang model analogy (1/t as t→0+)", x => 1 / x },  // Сингулярность в начале времени (x>0)

                { "L22: Black Hole Schwarzschild analogy (1/(1 - 2M/r) at r=2M)", x => 1 / (1 - x) },  // Координатная сингулярность на горизонте (x=1)
                { "L23: Burgers equation blow-up model (1/(T - t))", x => 1 / (1 - x) },
                { "L24: Nested Singularity (x / (x * x)) ",  x => (x / (x * x))},
                { "L25: Твой тест джемини  ",  x => (x*2 / x)}
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
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== RICIS Symbolic Engine ===\n");
        Console.WriteLine("Интерактивный режим: вводите выражение с переменной x (например, 1 / (x*(x+1)))");
        Console.WriteLine("Для выхода введите 'exit' или 'quit'\n");

        while (true)
        {
            Console.Write("Выражение> ");
            string input = Console.ReadLine()?.Trim();

            if (string.IsNullOrEmpty(input) || input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            //try
            //{
            //    // Парсим введённое выражение как Lambda: x => <input>
            //    var param = Expression.Parameter(typeof(double), "x");
            //    var expr = System.Linq.Dynamic.Core.DynamicExpressionParser.ParseLambda(
            //        new[] { param }, typeof(double), input).Body;

            //    Console.WriteLine($"Введено: x => {expr}");

            //    // Упрощаем по RICIS
            //    var simplified = ExpressionSimplifier.Simplify(expr);

            //    // Вывод результата
            //    Console.ForegroundColor = ConsoleColor.Cyan;
            //    Console.WriteLine($"Результат RICIS: x => {simplified}");
            //    Console.ResetColor();

            //    // Полярное представление, если есть ∞ или Monolith
            //    if (simplified is InfinityExpression inf)
            //    {
            //        Console.WriteLine(PolarConverter.ToPolarSector(inf, totalSectors: 8));
            //    }
            //    else if (simplified is SingularityMonolithExpression mono)
            //    {
            //        Console.WriteLine(PolarConverter.ToPolarSector(mono, totalSectors: 8));
            //    }
            //}
            //catch (Exception ex)
            //{
            //    Console.ForegroundColor = ConsoleColor.Red;
            //    Console.WriteLine($"Ошибка парсинга или анализа: {ex.Message}");
            //    Console.ResetColor();
            //}

            Console.WriteLine(new string('-', 60));
        }

        Console.WriteLine("RICIS Symbolic Engine завершил работу.");
    }
}