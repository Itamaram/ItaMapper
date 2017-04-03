using System;
using System.Linq.Expressions;

namespace IttyMapper.Exceptions
{
    public class NotMemberExpressionException : Exception
    {
        public NotMemberExpressionException(Expression expression)
            : base($"Expression '{expression}' was not a member expression (of the form x => x.Foo)") { }
    }
}