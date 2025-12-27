using System.Linq.Expressions;

internal static class LinearExtractor
{
    public static (double multiplier, double offset)? Extract(Expression expr, ParameterExpression param)
    {
        // Поддержка: a * param + b, a * param, param, const
        var visitor = new LinearVisitor(param);
        visitor.Visit(expr);
        return visitor.Success ? visitor.Result : null;
    }

    private class LinearVisitor : ExpressionVisitor
    {
        private readonly ParameterExpression _param;
        public bool Success = false;
        public (double multiplier, double offset) Result;

        public LinearVisitor(ParameterExpression param) => _param = param;

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.Add || node.NodeType == ExpressionType.Subtract)
            {
                // Ищем param-содержащую часть и константу
                double coeff = 0, constant = 0;
                ExtractLinear(node.Left, ref coeff, ref constant);
                double sign = node.NodeType == ExpressionType.Subtract ? -1 : 1;
                ExtractLinear(node.Right, sign, ref coeff, ref constant);

                if (Success = true)
                    Result = (coeff, constant);
                return node;
            }

            if (node.NodeType == ExpressionType.Multiply)
            {
                if (node.Left is ConstantExpression c && node.Right is ParameterExpression p && p == _param)
                {
                    Success = true;
                    Result = (Convert.ToDouble(c.Value), 0);
                    return node;
                }
            }

            return base.VisitBinary(node);
        }

        private void ExtractLinear(Expression ex, double sign, ref double coeff, ref double constant)
        {
            if (ex is ParameterExpression p && p == _param) coeff += sign;
            else if (ex is ConstantExpression c) constant += sign * Convert.ToDouble(c.Value);
        }

        private void ExtractLinear(Expression ex, ref double coeff, ref double constant)
        {
            ExtractLinear(ex, 1.0, ref coeff, ref constant);
        }
    }
}