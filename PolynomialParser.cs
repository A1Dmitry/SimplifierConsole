using System;
using System.Linq.Expressions;
using System.Collections.Generic;
using System.Linq;

namespace PolynomialProcessing
{
    /// <summary>
    /// Robust quadratic extractor: walks the expression, collects additive terms,
    /// for each term extracts coefficient and degree (power of the single parameter).
    /// Returns (parameter, a, b, c) for a*x^2 + b*x + c if found (otherwise null).
    /// </summary>
    public static class PolynomialParser
    {
        public static (ParameterExpression, double a, double b, double c)? ParseQuadratic(Expression expr)
        {
            if (expr == null) return null;

            // collect terms as (degree -> coefficient)
            var terms = new Dictionary<int, double>();
            ParameterExpression variable = null;

            // flatten sum/sub expression into terms
            var additiveTerms = FlattenAddSubtract(expr);

            foreach (var term in additiveTerms)
            {
                if (TryExtractMonomial(term, ref variable, out var degree, out var coeff))
                {
                    if (!terms.ContainsKey(degree)) terms[degree] = 0.0;
                    terms[degree] += coeff;
                }
                else
                {
                    // unsupported term (function call etc.) — can't parse quadratic
                    return null;
                }
            }

            // now map degrees to a,b,c
            double a = terms.ContainsKey(2) ? terms[2] : 0.0;
            double b = terms.ContainsKey(1) ? terms[1] : 0.0;
            double c = terms.ContainsKey(0) ? terms[0] : 0.0;

            // if no variable found or all zero — return null
            if (variable == null) return null;
            if (Math.Abs(a) < double.Epsilon && Math.Abs(b) < double.Epsilon && Math.Abs(c) < double.Epsilon) return null;

            return (variable, a, b, c);
        }

        private static List<Expression> FlattenAddSubtract(Expression expr)
        {
            var list = new List<Expression>();
            void Recur(Expression e, double sign = 1.0)
            {
                if (e == null) return;

                if (e.NodeType == ExpressionType.Add)
                {
                    var b = (BinaryExpression)e;
                    Recur(b.Left, sign);
                    Recur(b.Right, sign);
                    return;
                }

                if (e.NodeType == ExpressionType.Subtract)
                {
                    var b = (BinaryExpression)e;
                    Recur(b.Left, sign);
                    Recur(b.Right, -sign);
                    return;
                }

                // preserve sign by creating Constant multiply if sign == -1
                if (sign == -1.0)
                {
                    // create -1 * e
                    list.Add(Expression.Multiply(Expression.Constant(-1.0), e));
                }
                else
                {
                    list.Add(e);
                }
            }

            Recur(expr);
            return list;
        }

        /// <summary>
        /// Tries to parse a monomial term, returns degree (0,1,2,...) and numeric coefficient.
        /// Accepts forms:
        /// - constant
        /// - constant * x
        /// - x * constant
        /// - x * x
        /// - constant * x * x (any order)
        /// - nested multiplies
        /// If finds a parameter expression, sets variable (if not set) and ensures it's same param.
        /// </summary>
        private static bool TryExtractMonomial(Expression expr, ref ParameterExpression variable, out int degree, out double coefficient)
        {
            degree = 0;
            coefficient = 1.0;

            // walk multiplicative factors
            var factors = new List<Expression>();
            CollectMultiplicativeFactors(expr, factors);

            // if expr was a plain parameter or constant, factors will contain it
            foreach (var f in factors)
            {
                if (f is ConstantExpression c)
                {
                    if (!TryGetNumericConstant(c, out var v)) return false;
                    coefficient *= v;
                    continue;
                }

                if (f is ParameterExpression p)
                {
                    if (variable == null) variable = p;
                    else if (variable != p) return false; // multiple different parameters -> fail
                    degree += 1;
                    continue;
                }

                // handle parenthesized expressions like (x * x) represented as BinaryExpression Multiply
                if (f is BinaryExpression be && be.NodeType == ExpressionType.Multiply)
                {
                    // recursively attempt to extract monomial — flatten should have decomposed it, so this likely won't happen
                    if (!TryExtractMonomial(f, ref variable, out var d2, out var c2)) return false;
                    degree += d2;
                    coefficient *= c2;
                    continue;
                }

                // unsupported factor (method call, pow, etc.)
                return false;
            }

            return true;
        }

        private static void CollectMultiplicativeFactors(Expression expr, List<Expression> outFactors)
        {
            if (expr == null) return;

            if (expr.NodeType == ExpressionType.Multiply)
            {
                var b = (BinaryExpression)expr;
                CollectMultiplicativeFactors(b.Left, outFactors);
                CollectMultiplicativeFactors(b.Right, outFactors);
                return;
            }

            outFactors.Add(expr);
        }

        private static bool TryGetNumericConstant(ConstantExpression c, out double value)
        {
            value = 0.0;
            if (c.Value == null) return false;
            switch (Type.GetTypeCode(c.Value.GetType()))
            {
                case TypeCode.Int32:
                case TypeCode.Int64:
                case TypeCode.Double:
                case TypeCode.Decimal:
                case TypeCode.Single:
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.UInt16:
                case TypeCode.UInt32:
                case TypeCode.UInt64:
                case TypeCode.Int16:
                    try
                    {
                        value = Convert.ToDouble(c.Value);
                        return true;
                    }
                    catch { return false; }
                default:
                    return false;
            }
        }
    }
}