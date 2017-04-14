using System;
using System.Linq.Expressions;
using System.Reflection;
using ItaMapper.Exceptions;

namespace ItaMapper.Extensions
{
    public static class ExpressionExtensions
    {
        public static UnaryExpression Convert(this Expression e, Type t)
        {
            return Expression.Convert(e, t);
        }

        public static MemberExpression GetMemberExpression<A, B>(this Expression<Func<A, B>> expression)
        {
            //Test to see if the expression is explicitly converted to object
            var body = (expression.Body is UnaryExpression unary && unary.NodeType == ExpressionType.Convert)
                ? unary.Operand
                : expression.Body;

            return body as MemberExpression ?? throw new NotMemberExpressionException(expression);
        }

        public static PropertyInfo GetPropertyInfo(this MemberExpression expression)
        {
            return expression.Member as PropertyInfo ?? throw new NotPropertyExpressionExpresion(expression);
        }
    }
}