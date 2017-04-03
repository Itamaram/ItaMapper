using System;
using System.Linq.Expressions;

namespace IttyMapper.Exceptions
{
    public class NotPropertyExpressionExpresion : Exception
    {
        public NotPropertyExpressionExpresion(MemberExpression expression)
            : base($"Member Expression '{expression}' is not a Property") { }
    }
}