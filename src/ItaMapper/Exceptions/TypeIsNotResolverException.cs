using System;

namespace ItaMapper.Exceptions
{
    public class TypeIsNotResolverException<A,B> : Exception
    {
        public TypeIsNotResolverException(Type type)
            :base($"The type '{type}' must be assignable from {typeof(ValueResolver<A, B>)}, but it was not")
        {
        }
    }
}