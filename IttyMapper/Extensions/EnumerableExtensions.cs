using System;
using System.Collections.Generic;
using System.Linq;

namespace IttyMapper.Extensions
{
    public static class EnumerableExtensions
    {
        public static void ForEach<A>(this IEnumerable<A> items, Action<A> action)
        {
            foreach (var item in items)
                action(item);
        }

        public static IEnumerable<A> Yield<A>(this A a)
        {
            yield return a;
        }

        public static IEnumerable<A> Append<A>(this IEnumerable<A> items, A item)
        {
            return items.Concat(item.Yield());
        }

        public static IEnumerable<A> AppendIfNotNull<A>(this IEnumerable<A> items, A item)
            where A : class
        {
            return items.Concat(item.Yield().Where(a => a != null));
        }
    }
}