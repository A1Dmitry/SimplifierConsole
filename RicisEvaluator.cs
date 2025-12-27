using System.Linq.Expressions;

namespace RicisCore;

public class RicisEvaluator : ExpressionVisitor
{
    private readonly Dictionary<string, RicisEntity> _context;

    public RicisEvaluator(Dictionary<string, RicisEntity> context)
    {
        _context = context;
    }

    public RicisEntity Evaluate(Expression expr)
    {
        // Запускаем визитор, он вернет ConstantExpression с RicisEntity внутри
        var resultExpr = Visit(expr);
        if (resultExpr is ConstantExpression { Value: RicisEntity re })
            return re;

        throw new Exception("Evaluation failed to produce a RicisEntity");
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        var left = Evaluate(node.Left);
        var right = Evaluate(node.Right);

        RicisEntity result;
        switch (node.NodeType)
        {
            case ExpressionType.Add:
                result = left + right;
                break;
            case ExpressionType.Subtract:
                result = left - right;
                break;
            case ExpressionType.Multiply:
                result = left * right;
                break;
            case ExpressionType.Divide:
                result = left / right;
                break;
            default: throw new NotSupportedException($"Op {node.NodeType} not supported in RICIS");
        }

        return Expression.Constant(result);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        // Обработка переменных из замыкания (closure)
        if (_context.ContainsKey(node.Member.Name))
            return Expression.Constant(_context[node.Member.Name]);

        // Если это поле класса
        var objectMember = Expression.Convert(node, typeof(object));
        var getterLambda = Expression.Lambda<Func<object>>(objectMember);
        var getter = getterLambda.Compile();
        var value = getter();

        if (value is int i) return Expression.Constant(new RicisEntity(i, RicisType.Scalar));
        if (value is double d) return Expression.Constant(new RicisEntity(d, RicisType.Scalar));

        throw new Exception($"Unknown variable: {node.Member.Name}");
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        if (node.Value is int i) return Expression.Constant(new RicisEntity(i, RicisType.Scalar));
        if (node.Value is double d) return Expression.Constant(new RicisEntity(d, RicisType.Scalar));
        return base.VisitConstant(node);
    }
}