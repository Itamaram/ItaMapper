using System;

namespace ItaMapper.Extensions
{
    public static class GenericTypeExtensions
    {
        public static bool IsOpenGeneric(this Type type)
        {
            return type == typeof(Array) || type.IsGenericTypeDefinition;
        }

        public static int CountOpenGenerics(this Type type)
        {
            return type.IsArray ? 1 : type.GetGenericArguments().Length;
        }

        public static Type[] GetClosedGenerics(this Type type)
        {
            return type.IsArray ? new[] { type.GetElementType() } : type.GenericTypeArguments;
        }

        public static Type MakeGeneric(this Type type, Type[] args)
        {
            if (!type.IsArray)
                return type.MakeGenericType(args);

            if (args.Length != 1)
                throw new Exception(/*todo*/);

            return Array.CreateInstance(args[0], 0).GetType();
        }
    }
}