using System.Linq.Expressions;

public static class SingularitySolver
{
    // Возвращает список корней, так как у квадратного уравнения их может быть 2
    public static List<(ParameterExpression, double)> SolveRoot(Expression expr)
    {
        var roots = new List<(ParameterExpression, double)>();

        // 1. Пытаемся собрать коэффициенты полинома: ax^2 + bx + c
        // Возвращает (Parameter, a, b, c)
        var poly = PolynomialParser.ParseQuadratic(expr);

        if (poly.HasValue)
        {
            var (param, a, b, c) = poly.Value;

            // СЛУЧАЙ 1: Линейное уравнение (a=0): bx + c = 0 -> x = -c/b
            if (Math.Abs(a) < 1e-10)
            {
                if (Math.Abs(b) > 1e-10)
                {
                    roots.Add((param, -c / b));
                }
            }
            // СЛУЧАЙ 2: Квадратное уравнение: ax^2 + bx + c = 0
            else
            {
                double D = b * b - 4 * a * c; // Дискриминант

                if (D >= 0)
                {
                    double sqrtD = Math.Sqrt(D);
                    double x1 = (-b + sqrtD) / (2 * a);
                    double x2 = (-b - sqrtD) / (2 * a);

                    roots.Add((param, x1));

                    // Если корни разные, добавляем второй
                    if (Math.Abs(x1 - x2) > 1e-10)
                    {
                        roots.Add((param, x2));
                    }
                }
            }
        }

        return roots;
    }
}

// Парсер, который превращает дерево выражений в коэффициенты a, b, c
public static class PolynomialParser
{
    public static (ParameterExpression, double a, double b, double c)? ParseQuadratic(Expression expr)
    {
        // Рекурсивно собираем коэффициенты
        // Поддерживает: x*x - 4,  2*x - 6,  x^2 + 2x + 1

        var visitor = new CoefficientsVisitor();
        visitor.Visit(expr);

        if (visitor.Variable == null) return null; // Не нашли переменную

        return (visitor.Variable, visitor.A, visitor.B, visitor.C);
    }

    private class CoefficientsVisitor : ExpressionVisitor
    {
        public ParameterExpression Variable { get; private set; }
        public double A { get; private set; } = 0; // x^2
        public double B { get; private set; } = 0; // x
        public double C { get; private set; } = 0; // const

        // Множитель для текущей ветки (нужен для обработки вычитания: x - 5 это x + (-5))
        private double _currentSign = 1.0;

        public override Expression Visit(Expression node)
        {
            if (node == null) return null;

            // Константа
            if (node is ConstantExpression c)
            {
                C += Convert.ToDouble(c.Value) * _currentSign;
                return node;
            }

            // Переменная (x) -> b += 1
            if (node is ParameterExpression p)
            {
                if (Variable == null) Variable = p;
                if (Variable == p) B += 1.0 * _currentSign;
                return node;
            }

            // Умножение (2*x, x*2, x*x)
            if (node.NodeType == ExpressionType.Multiply)
            {
                var bin = (BinaryExpression)node;

                // x * x -> a += 1
                if (bin.Left is ParameterExpression && bin.Right is ParameterExpression)
                {
                    A += 1.0 * _currentSign;
                    return node;
                }

                // 2 * x
                if (bin.Left is ConstantExpression cLeft && bin.Right is ParameterExpression)
                {
                    B += Convert.ToDouble(cLeft.Value) * _currentSign;
                    return node;
                }
                // x * 2
                if (bin.Left is ParameterExpression && bin.Right is ConstantExpression cRight)
                {
                    B += Convert.ToDouble(cRight.Value) * _currentSign;
                    return node;
                }
            }

            // Сложение / Вычитание (рекурсия)
            if (node.NodeType == ExpressionType.Add || node.NodeType == ExpressionType.Subtract)
            {
                var bin = (BinaryExpression)node;
                Visit(bin.Left);

                // Для правой части учитываем знак операции
                double savedSign = _currentSign;
                if (node.NodeType == ExpressionType.Subtract) _currentSign *= -1;

                Visit(bin.Right);

                _currentSign = savedSign; // Восстанавливаем знак
                return node;
            }

            return base.Visit(node);
        }
    }
}