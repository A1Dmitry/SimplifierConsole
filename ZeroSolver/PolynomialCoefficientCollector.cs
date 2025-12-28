using System.Linq.Expressions;
using System.Numerics;

namespace SimplifierConsole.ZeroSolver;

public class PolynomialCoefficientCollector : ExpressionVisitor
{
    private readonly ParameterExpression _parameter;
    private Rational _currentMultiplier = Rational.One; // Текущий коэффициент перед параметром
    private int _currentPower = -1; // Текущая степень (при умножении)

    public PolynomialCoefficientCollector(ParameterExpression parameterExpression)
    {
        _parameter = parameterExpression ?? throw new ArgumentNullException(nameof(parameterExpression));
    }

    public bool IsPolynomial { get; private set; } = true;
    public Dictionary<int, Rational> Coefficients { get; } = new();

    public void Visit(Expression expr)
    {
        IsPolynomial = true;
        Coefficients.Clear();
        base.Visit(expr);

        // Если в процессе посещения что-то сломало IsPolynomial, оставляем пустой результат
        if (!IsPolynomial)
            Coefficients.Clear();
    }

    protected override Expression VisitParameter(ParameterExpression node)
    {
        if (node == _parameter)
        {
            // Параметр в степени _currentPower с коэффициентом _currentMultiplier
            if (_currentPower < 0) _currentPower = 1; // одиночный x → x^1

            AddToCoefficients(_currentPower, _currentMultiplier);
        }
        else
        {
            // Параметр с другим именем — не полином по нашему x
            IsPolynomial = false;
        }

        return node;
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        var value = ConvertConstantToRational(node.Value);

        if (_currentPower < 0)
            // Константа сама по себе — степень 0
            AddToCoefficients(0, value);
        else
            // Константа умножается на текущую степень параметра
            AddToCoefficients(_currentPower, _currentMultiplier * value);

        return node;
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        if (!IsPolynomial) return node;

        switch (node.NodeType)
        {
            case ExpressionType.Add:
            case ExpressionType.Subtract:
                // Обрабатываем как (левая часть) + (правая часть)
                var savedState = SaveState();

                Visit(node.Left);

                var leftState = SaveState();
                RestoreState(savedState);

                // Для правой части учитываем знак
                if (node.NodeType == ExpressionType.Subtract)
                    _currentMultiplier = -_currentMultiplier;

                Visit(node.Right);

                RestoreState(leftState); // не обязательно, но для чистоты
                break;

            case ExpressionType.Multiply:
                // Умножение полиномов: собираем степени
                VisitMultiply(node);
                break;

            default:
                IsPolynomial = false;
                break;
        }

        return node;
    }

    private void VisitMultiply(BinaryExpression node)
    {
        // Сохраняем состояние перед левой частью
        var outerState = SaveState();

        // Собираем левую часть как отдельный полином
        Visit(node.Left);
        if (!IsPolynomial) return;

        var leftCoeffs = new Dictionary<int, Rational>(Coefficients);
        var leftPower = _currentPower;
        var leftMult = _currentMultiplier;

        // Восстанавливаем состояние
        RestoreState(outerState);
        Coefficients.Clear();

        // Собираем правую часть
        Visit(node.Right);
        if (!IsPolynomial) return;

        var rightCoeffs = new Dictionary<int, Rational>(Coefficients);

        // Теперь перемножаем два полинома
        Coefficients.Clear();

        foreach (var left in leftCoeffs)
        foreach (var right in rightCoeffs)
        {
            var newPower = left.Key + right.Key;
            var newCoeff = left.Value * right.Value;
            AddToCoefficients(newPower, newCoeff);
        }

        // Сбрасываем временные состояния
        _currentPower = -1;
        _currentMultiplier = Rational.One;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        if (node.NodeType == ExpressionType.Negate)
        {
            _currentMultiplier = -_currentMultiplier;
            Visit(node.Operand);
            _currentMultiplier = -_currentMultiplier; // восстановить знак
            return node;
        }

        IsPolynomial = false;
        return node;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        // Любые вызовы методов (Sin, Cos, Pow и т.д.) — не полином
        IsPolynomial = false;
        return node;
    }

    // ===================================================================
    // Вспомогательные методы
    // ===================================================================

    private void AddToCoefficients(int power, Rational coeff)
    {
        if (coeff.IsZero) return; // не добавляем нулевые коэффициенты

        if (Coefficients.TryGetValue(power, out var existing))
            coeff += existing;

        if (coeff.IsZero)
            Coefficients.Remove(power);
        else
            Coefficients[power] = coeff;
    }

    private Rational ConvertConstantToRational(object value)
    {
        if (value is double db)
        {
            // Обработка double отдельно
            var intValue = (long)Math.Round(db);
            if (Math.Abs(db - intValue) == 0)
            {
                return Rational.Create(intValue);
            }

            // Нецелый double — запрещаем в полиномах
            IsPolynomial = false;
            return Rational.Zero; // заглушка, полином уже невалиден
        }

        // Остальные типы — через switch expression (без блоков {})
        return value switch
        {
            int i => Rational.Create(i),
            long l => Rational.Create(l),
            BigInteger bi => new Rational(bi),
            decimal d => Rational.FromDecimal(d),
            null => throw new ArgumentNullException(nameof(value)),
            _ => throw new ArgumentException($"Unsupported constant type: {value?.GetType()}")
        };
    }

    private (int power, Rational mult, Dictionary<int, Rational> coeffs) SaveState()
    {
        return (_currentPower, _currentMultiplier, new Dictionary<int, Rational>(Coefficients));
    }
    
    private void RestoreState((int power, Rational mult, Dictionary<int, Rational> coeffs) state)
    {
        _currentPower = state.power;
        _currentMultiplier = state.mult;
        Coefficients.Clear();
        foreach (var kv in state.coeffs)
            Coefficients[kv.Key] = kv.Value;
    }
}