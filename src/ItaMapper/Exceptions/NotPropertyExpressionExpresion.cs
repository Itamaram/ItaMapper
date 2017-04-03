using System;
using System.Linq.Expressions;

namespace ItaMapper.Exceptions
{
    public class NotPropertyExpressionExpresion : Exception
    {
        public NotPropertyExpressionExpresion(MemberExpression expression)
            : base($"Member Expression '{expression}' is not a Property") { }
    }
}