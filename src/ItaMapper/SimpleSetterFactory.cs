using System;
using System.Reflection;
using ItaMapper.Extensions;

namespace ItaMapper
{
    public interface SimpleSetterFactory
    {
        Action<object, object> SetterFor(Type type, string member);
    }

    public class ReflectionSetterFactory : SimpleSetterFactory
    {
        public Action<object, object> SetterFor(Type type, string member)
        {
            return type.GetProperty(member).SetMethod.Map(Invoke);
        }

        private static Action<object, object> Invoke(MethodInfo mi) => (x, y) => mi.Invoke(x, new[] { y });
    }

    public class ExpressionSetterFactory : SimpleSetterFactory
    {
        public Action<object, object> SetterFor(Type type, string member)
        {
            return new ExpressionBuilder().Setter(type, member);
        }
    }
}