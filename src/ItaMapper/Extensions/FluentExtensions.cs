using System;

namespace ItaMapper.Extensions
{
    internal static class FluentExtensions
    {
        public static A Do<A>(this A a, Action<A> action)
        {
            action(a);
            return a;
        }

        public static B Pipe<A, B>(this A a, Func<A, B> map) => map(a);

        public static A Cast<A>(this object o) => (A) o;
    }
}