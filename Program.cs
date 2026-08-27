using System.Linq.Dynamic.Core;
using System.Linq.Dynamic.Core.Exceptions;
using System.Linq.Expressions;
using System.Text;
using System.Text.RegularExpressions;
using Ricis.Core;
using Ricis.Core.Expressions;
using Ricis.Core.Phases;

namespace SimplifierConsole;

internal class Program
{
    private static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("--- RICIS Symbolic Engine: Deep Stress Test ---\n");

        var testCases = new Dictionary<string, Expression<Func<double, double>>>
        {
            { "L0: Basic Singularity (10 / (x - 2))", x => 10 / (x - 2) },

            { "L1: Removable Squares ((x^2 - 25)/(x - 5))", x => (Math.Pow(x, 2) - 25) / (x - 5) },

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
            {
                "L15: Essential Singularity (exp(1/z) at z=0)", x => Math.Exp(1 / x)
            }, // Классический essential singularity

            { "L16: Simple Pole (1/z at z=0)", x => 1 / x },

            { "L17: Pole of order 2 (1/z² at z=0)", x => 1 / (x * x) },

            {
                "L18: Logarithmic Singularity (Log(z) at z=0)", x => Math.Log(x)
            }, // Branch point, но полюс в мнимой части

            { "L19: Removable Singularity classic (sin(x)/x at x=0)", x => Math.Sin(x) / x }, // Уже был, но с именем

            { "L20: Picard Theorem example (exp(1/z) essential)", x => Math.Exp(1 / x) }, // Повтор для демонстрации

            { "L21: Big Bang model analogy (1/t as t→0+)", x => 1 / x }, // Сингулярность в начале времени (x>0)

            {
                "L22: Black Hole Schwarzschild analogy (1/(1 - 2M/r) at r=2M)", x => 1 / (1 - x)
            }, // Координатная сингулярность на горизонте (x=1)
            { "L23: Burgers equation blow-up model (1/(T - t))", x => 1 / (1 - x) },
            { "L24: Nested Singularity (x / (x * x)) ", x => x / (x * x) },
            {
                "L25: blow-up singularity: 1 /(Math.Cos(x) * Math.Sinh(x) -1)  ",
                x => 1 / (Math.Cos(x) * Math.Sinh(x) - 1)
            },
            { "L26: POW  ", x => 1 / (Math.Pow(x, 4) - 1) },
            {
                "L27: Pole order 3 (1/z³ at z=0)", x => 1 / (x * x * x)
            },

            {
                "L28: Triple root removable ((x³-8)/(x-2))", x => (Math.Pow(x, 3) - 8) / (x - 2)
            },

            {
                "L29: Double pole quadratic (1/(x²(x-1)))", x => 1 / (x * x * (x - 1))
            },

            {
                "L30: Branch point sqrt(z) at z=0", x => Math.Sqrt(x)
            },

            {
                "L31: Γ(z) pole reflection (sin(πz)/z)", x => Math.Sin(Math.PI * x) / x // Γ(z) pole proxy
            },

            {
                "L32: Bessel J₀(z) essential at ∞", x => Math.Sqrt(2 / (Math.PI * x)) * Math.Cos(x - Math.PI/4)
            },

            {
                "L33: Airy Ai(z) Stokes line", x => Math.Exp(Math.Pow(x, 1.5))
            },

            {
                "L34: Riemann ζ(z) pole proxy at z=1", x => 1 / (x - 1) + Math.Log(Math.Abs(x))
            },

            {
                "L35: Fermi-Dirac integral", x => Math.Log(1 + Math.Exp(-1 / x)) / x
            },

            {
                "L36: NS 3D blow-up (T-t)^{-1/3}", x => 1 / Math.Pow(1 - x, 2.0/3)
            },

            {
                "L37: Euler vortex sheet", x => 1 / Math.Sqrt(Math.Abs(x) + double.Epsilon)
            },

            {
                "L38: Schwarzschild g_{00}", x => 1 / (1 - 2 / x)
            },

            {
                "L39: QCD instanton action", x => Math.Exp(-8 * Math.PI * Math.PI / x)
            },

            {
                "L40: Casimir c/12 UV pole", x => 1 / (12 * x)
            },

            {
                "L41: Yang-Mills monopole", x => 1 / Math.Sqrt(x * x + double.Epsilon)

            },
             // --- НОВЫЕ ТЕСТЫ (RICIS v7.4 Check) ---

            // L42: Требует 3-х кратного применения правила Лопиталя.
            // (x - sin(x)) / x^3 -> (1 - cos(x))/3x^2 -> sin(x)/6x -> cos(x)/6 -> 1/6 (0.1666...)
            { "L42: Higher Order L'Hopital ((x - sin(x))/x^3)", x => (x - Math.Sin(x)) / Math.Pow(x, 3) },

            // L43: Гиперболический 0/0. Проверка производных Sinh/Cosh.
            // (sinh(x) - x) / x^3 -> (cosh(x) - 1)/3x^2 -> sinh(x)/6x -> cosh(x)/6 -> 1/6
            { "L43: Hyperbolic Removable ((Sinh(x) - x)/x^3)", x => (Math.Sinh(x) - x) / Math.Pow(x, 3) },

            // L44: Тест на отсутствие корней (Regression для NaN).
            // x^2 + 1 = 0 -> x = ±i. Движок должен вернуть (NO SIMPLIFICATION).
            { "L44: Irreducible Quadratic (1 / (x^2 + 1))", x => 1 / (x * x + 1) },

            // L45: Смешанная трансцендентная сингулярность.
            // 1 - tan(x) = 0 -> tan(x) = 1 -> x = PI/4 (0.7853...)
            { "L45: Tangent Pole (1 / (1 - Tan(x)))", x => 1 / (1 - Math.Tan(x)) },

            // L46: Композитная экспонента.
            // e^(x^2) - 1 = 0 -> x^2 = 0 -> x = 0.
            // Это полюс 2-го порядка, но движок должен найти корень.
            { "L46: Composite Exponential Pole (1 / (Exp(x^2) - 1))", x => 1 / (Math.Exp(x * x) - 1) },

            // L47: Логарифмический ноль.
            // ln(x) = 0 -> x = 1.
            // 1 / ln(x) при x=1.
            { "L47: Logarithmic Zero (1 / Log(x))", x => 1 / Math.Log(x) },

            // L48: "Мягкая" сингулярность (Soft Singularity).
            // x * ln(x) при x -> 0. Это форма 0 * ∞.
            // Мы запишем как ln(x) / (1/x), чтобы проверить, справится ли Лопиталь с ∞/∞.
            // (ln x)' / (1/x)' = (1/x) / (-1/x^2) = -x -> 0.
            { "L48: Indeterminate form 0*Inf (Log(x) / (1/x))", x => Math.Log(x) / (1 / x) },

            // L49: Полином высокой степени.
            // x^5 - 32 = 0 -> x = 2.
            { "L49: Quintic Pole (1 / (x^5 - 32))", x => 1 / (Math.Pow(x, 5) - 32) },

            // L50: Стресс-тест точности (очень близкие корни).
            // (x - 1)(x - 1.0000001)
            { "L50: Precision Stress (1 / ((x - 1) * (x - 1.0000001)))", x => 1 / ((x - 1) * (x - 1.0000001)) }

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
                //var result = ExpressionSimplifier.Simplify(test.Value);
                var result = RicisPhasePipeline.Simplify(test.Value);
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
                    Console.ForegroundColor = ConsoleColor.Cyan;

                    Console.ResetColor();

                    // Полярное представление RICIS-III для ∞_F и Monolith.
                    // Generic pipeline preserves the enclosing lambda type, so
                    // the indexed singularity can only occur in its expression body.
                    if (result.Body is InfinityExpression infinity)
                    {
                        Console.ForegroundColor = ConsoleColor.Magenta;
                        Console.WriteLine(PolarConverter.ToPolarSector(infinity, 8));
                        Console.ResetColor();
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
        Console.WriteLine("=== RICIS Symbolic Engine v7.3 ===\n");
        Console.WriteLine("Interactive mode of the 5th stage");
        Console.WriteLine("Enter an expression with variable x (for example: x => 1 / (x*(x+1)))");
        Console.WriteLine("Commands: help, exit, quit\n");

        while (true)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("RICIS> ");
            Console.ResetColor();

            var input = Console.ReadLine()?.Trim();
            input = Regex.Replace(
                input,
                @"\b(Sin|Cos|Tan|Pow|Exp|Log)\b",
                "Math.$1"
            );
            if (string.IsNullOrEmpty(input))
            {
                continue;
            }

            if (input.Equals("exit", StringComparison.OrdinalIgnoreCase) ||
                input.Equals("quit", StringComparison.OrdinalIgnoreCase))
            {
                break;
            }

            if (input.Equals("help", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine("Supported operations: +, -, *, /, ^ (Power), Sin, Cos, Tan, Exp, Log и т.д.");
                Console.WriteLine("Examples:");
                Console.WriteLine("x => Math.Sin(x)/Math.Cos(x)");
                Console.WriteLine("x => (Math.Exp(x) - 1)/x");
                Console.WriteLine("x =>  1/(1 - Math.Pow(x, 2)");
                Console.WriteLine("""
                                  Write: x^2 as Math.Pow(x, 2)
                                         x^3 → Math.Pow(x, 3)
                                         x^4 → Math.Pow(x, 4)
                                   x => 1 / (Math.Pow(x, 2) + 1)
                                   x => 1 / Math.Cos(x)   // cot(x) = 1/tan(x) = cos(x)/sin(x), 
                                   x => 1 / Math.Sin(1/x)
                                   x => (Math.Pow(x, 3) - 8)/(x - 2)
                                   x => 1 / Math.Cos(x)   // sec(x)
                                   x => 1 / (Math.Pow(x, 4) - 1)
                                   x => Math.Sinh(x)/x
                                   x => 1 / (Math.Exp(x) - 1)
                                   x => (Math.Pow(x, 2) + x + 1)/(x + 1)
                                   x => 1 / Math.Tan(x - Math.PI/4)
                                   x => (Math.Cos(x) - 1)/x
                                   x => 1 / (Math.Pow(x, 3) + x)
                                   x => Math.Exp(-1/Math.Pow(x, 2))
                                  """);
                continue;
            }

            try
            {
                // Парсим как lambda: x => <input>
                var param = Expression.Parameter(typeof(double), "x");
                // Улучшенный парсинг с конфигурацией
                var config = new ParsingConfig
                {
                    IsCaseSensitive = false,
                    AllowNewToEvaluateAnyType = false
                };
                var lambda = DynamicExpressionParser.ParseLambda(config, new[] { param }, typeof(double), input);
                var expr = lambda.Body;

                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine($"Introduced: x => {expr}");
                Console.ResetColor();

                // Упрощаем через полный RICIS пайплайн
                var simplified = RicisPhasePipeline.Simplify(expr);

                // Основной результат
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"RICIS:   x => {simplified}");
                Console.ResetColor();

                // Полярное представление (5-я стадия)
                //if (simplified is InfinityExpression inf)
                //{
                //    Console.ForegroundColor = ConsoleColor.Magenta;
                //    Console.WriteLine(PolarConverter.ToPolarSector(inf, totalSectors: 8));
                //    Console.ResetColor();
                //}
            }
            catch (ParseException pex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                if (pex.HResult == -2146233088)
                {
                    var match = Regex.Match(pex.Message, "'([^']+)'");
                    var methodName = match.Groups[1].Value; // Cos
                    Console.WriteLine($"Syntax error: Use Math.{methodName} as function");
                }

                Console.ResetColor();
            }
            catch (Exception ex)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"Ошибка: {ex.Message}");
                Console.ResetColor();
            }

            Console.WriteLine(new string('-', 60));
        }

        Console.WriteLine("RICIS Symbolic Engine Finished work. Goodbye!");
    }
}