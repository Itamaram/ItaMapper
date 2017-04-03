using System;
using System.Linq.Expressions;
using System.Reflection;
using IttyMapper.Exceptions;

namespace IttyMapper.Extensions
{
    public static class ExpressionExtensions
    {
        public static UnaryExpression Convert(this Expression e, Type t)
        {
            return Expression.Convert(e, t);
        }

        public static MemberExpression GetMemberExpression<A, B>(this Expression<Func<A, B>> expression)
        {
            return expression.Body as MemberExpression ?? throw new NotMemberExpressionException(expression);
        }

        public static PropertyInfo GetPropertyInfo(this MemberExpression expression)
        {
            return expression.Member as PropertyInfo ?? throw new NotPropertyExpressionExpresion(expression);
        }
    }
}