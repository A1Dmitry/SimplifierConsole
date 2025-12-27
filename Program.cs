using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Text;


class Program
{
    static void Main()
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("--- RICIS Symbolic Engine: Stress Test ---\n");

        var testCases = new Dictionary<string, Expression<Func<double, double>>>
        {
            // УРОВЕНЬ 1: База
            { "L1: Basic Singularity (10 / (x - 2))", x => 10 / (x - 2) },
            { "L1: Removable Squares ((x^2 - 25)/(x - 5))", x => (x * x - 25) / (x - 5) },

            // УРОВЕНЬ 2: Коэффициенты
            { "L2: Coefficients (1 / (2x - 6))", x => 1 / (2 * x - 6) },

            // УРОВЕНЬ 3: Полиномы
            { "L3: Cubic Removable ((x^3 - 1)/(x - 1))", x => (x * x * x - 1) / (x - 1) },
            { "L3: Quadratic Denominator (1 / (x^2 - 4))", x => 1 / (x * x - 4) },

            // УРОВЕНЬ 4: Факторизация
            { "L4: Factoring ((x^2 - 2x) / (x - 2))", x => (x * x - 2 * x) / (x - 2) },

            // УРОВЕНЬ 5: Тригонометрия (НОВОЕ)
            { "L5: Trigonometry (tan(x) = sin/cos)", x => Math.Sin(x) / Math.Cos(x) },
        };

        int counter = 1;
        foreach (var test in testCases)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Test #{counter}: {test.Key}");
            Console.ResetColor();

            try
            {
                var result = ExpressionSimplifier.Simplify(test.Value);
                bool isUnchanged = result.ToString() == test.Value.ToString();

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