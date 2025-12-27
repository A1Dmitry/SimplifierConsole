using System.Linq.Expressions;

// PolynomialParser.cs
using System.Linq.Expressions;

public static class PolynomialParser
{
    public static (ParameterExpression, double a, double b, double c)? ParseQuadratic(Expression expr)
    {
        var visitor = new CoefficientsVisitor();
        visitor.Visit(expr);
        if (visitor.Variable == null) return null;
        return (visitor.Variable, visitor.A, visitor.B, visitor.C);
    }

    private class CoefficientsVisitor : ExpressionVisitor
    {
        public ParameterExpression Variable { get; private set; }
        public double A { get; private set; } // x²
        public double B { get; private set; } // x
        public double C { get; private set; } // константа

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Multiply)
            {
                // x * x → x²
                if (node.Left is ParameterExpression pl && node.Right is ParameterExpression pr && pl == pr)
                {
                    Variable ??= pl;
                    A += 1.0;
                    return node;
                }

                // x * (x + 1)
                if (node.Left is ParameterExpression paramLeft &&
                    node.Right is BinaryExpression addRight && addRight.NodeType == ExpressionType.Add)
                {
                    if (addRight.Left is ParameterExpression paramAdd &&
                        addRight.Right is ConstantExpression constRight &&
                        Math.Abs(Convert.ToDouble(constRight.Value) - 1.0) < 1e-10)
                    {
                        Variable ??= paramLeft;
                        A += 1.0;
                        B += 1.0;
                        return node;
                    }
                }

                // (x + 1) * x
                if (node.Right is ParameterExpression paramRight &&
                    node.Left is BinaryExpression addLeft && addLeft.NodeType == ExpressionType.Add)
                {
                    if (addLeft.Left is ParameterExpression paramAdd &&
                        addLeft.Right is ConstantExpression constLeft &&
                        Math.Abs(Convert.ToDouble(constLeft.Value) - 1.0) < 1e-10)
                    {
                        Variable ??= paramRight;
                        A += 1.0;
                        B += 1.0;
                        return node;
                    }
                }
            }
            else if (node.NodeType == ExpressionType.Subtract)
            {
                Visit(node.Left);
                // Для правой части вычитания — умножаем коэффициенты на -1
                var savedA = A;
                var savedB = B;
                var savedC = C;
                A = B = C = 0;
                Visit(node.Right);
                A = savedA - A;
                B = savedB - B;
                C = savedC - C;
                return node;
            }
            else if (node.NodeType == ExpressionType.Add )
            {
                Visit(node.Left);
                Visit(node.Right);
                return node;
            }

            return base.VisitBinary(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Variable ??= node;
            B += 1.0;
            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            C += Convert.ToDouble(node.Value);
            return node;
        }
    }
}