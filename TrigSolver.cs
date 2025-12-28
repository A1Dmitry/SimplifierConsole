using System;
using System.Linq.Expressions;

/// <summary>
/// Improved trig solver: supports composite linear arguments a*x + b (a,b constants).
/// Returns principal root for cos/sin/tan (can be extended to families).
/// </summary>
public static class TrigSolver
{
    public static (ParameterExpression, double)? Solve(Expression expr)
    {
        if (!(expr is MethodCallExpression call) || call.Arguments.Count == 0) return null;
        var arg = call.Arguments[0];

        // Direct simple parameter: sin(x), cos(x), tan(x)
        if (arg is ParameterExpression param)
        {
            if (call.Method.Name == "Cos") return (param, Math.PI / 2.0);
            if (call.Method.Name == "Sin") return (param, 0.0);
            if (call.Method.Name == "Tan") return (param, 0.0);
        }

        // Try extract linear: a*x + b
        if (TryExtractLinear(arg, out var paramExpr, out var a, out var b))
        {
            if (paramExpr == null || Math.Abs(a) < double.Epsilon) return null;

            if (call.Method.Name == "Cos")
            {
                // principal solution: a*x + b = PI/2  => x = (PI/2 - b)/a
                var root = (Math.PI / 2.0 - b) / a;
                return (paramExpr, root);
            }

            if (call.Method.Name == "Sin" || call.Method.Name == "Tan")
            {
                // principal solution: a*x + b = 0 => x = -b/a
                var root = (-b) / a;
                return (paramExpr, root);
            }
        }

        return null;
    }

    // Try to extract linear argument a*x + b from expression.
    // Supports forms: a*x, x*a, a*x + b, a*x - b, (x) etc.
    public static bool TryExtractLinear(Expression expr, out ParameterExpression param, out double a, out double b)
    {
        param = null; a = 1.0; b = 0.0;

        // strip unary convert (e.g. Convert(Constant,...))
        while (expr is UnaryExpression ue && (ue.NodeType == ExpressionType.Convert || ue.NodeType == ExpressionType.ConvertChecked))
            expr = ue.Operand;

        // a*x
        if (expr is BinaryExpression mul && mul.NodeType == ExpressionType.Multiply)
        {
            if (mul.Left is ConstantExpression c && mul.Right is ParameterExpression p)
            {
                if (!TryConst(c, out a)) return false;
                param = p;
                b = 0.0;
                return true;
            }
            if (mul.Right is ConstantExpression c2 && mul.Left is ParameterExpression p2)
            {
                if (!TryConst(c2, out a)) return false;
                param = p2;
                b = 0.0;
                return true;
            }
        }

        // a*x + b  or a*x - b
        if (expr is BinaryExpression add && (add.NodeType == ExpressionType.Add || add.NodeType == ExpressionType.Subtract))
        {
            Expression left = add.Left, right = add.Right;
            double sign = add.NodeType == ExpressionType.Subtract ? -1.0 : 1.0;

            // left = a*x, right = const
            if (left is BinaryExpression leftMul && leftMul.NodeType == ExpressionType.Multiply && right is ConstantExpression rc)
            {
                if (leftMul.Left is ConstantExpression cL && leftMul.Right is ParameterExpression pR)
                {
                    if (!TryConst(cL, out a)) return false;
                    param = pR;
                    if (!TryConst(rc, out var rcVal)) return false;
                    b = sign * rcVal;
                    return true;
                }
                if (leftMul.Right is ConstantExpression cR2 && leftMul.Left is ParameterExpression pL)
                {
                    if (!TryConst(cR2, out a)) return false;
                    param = pL;
                    if (!TryConst(rc, out var rcVal2)) return false;
                    b = sign * rcVal2;
                    return true;
                }
            }

            // right = a*x, left = const
            if (right is BinaryExpression rightMul && rightMul.NodeType == ExpressionType.Multiply && left is ConstantExpression lc)
            {
                if (rightMul.Left is ConstantExpression cL2 && rightMul.Right is ParameterExpression pR2)
                {
                    if (!TryConst(cL2, out a)) return false;
                    param = pR2;
                    if (!TryConst(lc, out var lcVal)) return false;
                    b = lcVal; // add or subtract handled by sign earlier
                    if (add.NodeType == ExpressionType.Subtract) b = -b;
                    return true;
                }
                if (rightMul.Right is ConstantExpression cR3 && rightMul.Left is ParameterExpression pL3)
                {
                    if (!TryConst(cR3, out a)) return false;
                    param = pL3;
                    if (!TryConst(lc, out var lcVal2)) return false;
                    b = lcVal2;
                    if (add.NodeType == ExpressionType.Subtract) b = -b;
                    return true;
                }
            }
        }

        // pure parameter
        if (expr is ParameterExpression pPure)
        {
            param = pPure;
            a = 1.0; b = 0.0;
            return true;
        }

        return false;
    }

    private static bool TryConst(ConstantExpression c, out double value)
    {
        value = 0.0;
        if (c == null || c.Value == null) return false;
        try
        {
            value = Convert.ToDouble(c.Value);
            return true;
        }
        catch
        {
            return false;
        }
    }
}