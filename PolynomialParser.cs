using System.Linq.Expressions;

public static class PolynomialParser
{
    public static (ParameterExpression, double a, double b, double c)? ParseQuadratic(Expression expr)
    {
        var visitor = new CoefficientsVisitor();
        visitor.Visit(expr);
        if (visitor.Variable == null) return null;
        if (visitor.A == 0 && visitor.B == 0 && visitor.C == 0) return null;
        return (visitor.Variable, visitor.A, visitor.B, visitor.C);
    }

    private class CoefficientsVisitor : ExpressionVisitor
    {
        private double _currentSign = 1.0;
        public ParameterExpression Variable { get; private set; }
        public double A { get; private set; }
        public double B { get; private set; }
        public double C { get; private set; }

        public override Expression Visit(Expression node)
        {
            if (node == null) return null;
            if (node is ConstantExpression c)
            {
                C += Convert.ToDouble(c.Value) * _currentSign;
                return node;
            }

            if (node is ParameterExpression p)
            {
                if (Variable == null) Variable = p;
                if (Variable == p) B += 1.0 * _currentSign;
                return node;
            }

            if (node.NodeType == ExpressionType.Multiply)
            {
                var bin = (BinaryExpression)node;
                if (bin.Left is ParameterExpression pL1 && bin.Right is ParameterExpression pR1)
                {
                    if (Variable == null) Variable = pL1;
                    A += 1.0 * _currentSign;
                    return node;
                }

                if (bin.Left is ConstantExpression cL2 && bin.Right is ParameterExpression pR2)
                {
                    if (Variable == null) Variable = pR2;
                    B += Convert.ToDouble(cL2.Value) * _currentSign;
                    return node;
                }

                if (bin.Left is ParameterExpression pL3 && bin.Right is ConstantExpression cR3)
                {
                    if (Variable == null) Variable = pL3;
                    B += Convert.ToDouble(cR3.Value) * _currentSign;
                    return node;
                }
            }

            if (node.NodeType == ExpressionType.Add || node.NodeType == ExpressionType.Subtract)
            {
                var bin = (BinaryExpression)node;
                Visit(bin.Left);
                var savedSign = _currentSign;
                if (node.NodeType == ExpressionType.Subtract) _currentSign *= -1;
                Visit(bin.Right);
                _currentSign = savedSign;
                return node;
            }

            if (node.NodeType == ExpressionType.Call) return node; // Игнорируем вызовы методов
            return base.Visit(node);
        }
    }
}