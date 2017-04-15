using System;
using System.Collections.Generic;
using System.Linq;

namespace ItaMapper
{
    public interface TypeMapDictionary
    {
        bool TryGet(Type source, Type destination, out TypeMap map);
    }

    public class ValueTupleTypeMapDictionary : TypeMapDictionary
    {
        private readonly IReadOnlyDictionary<(Type, Type), TypeMap> store;

        public ValueTupleTypeMapDictionary(IEnumerable<TypeMap> maps)
        {
            store = maps
                .GroupBy(m => (m.Source, m.Destination))
                .ToDictionary(g => g.Key, g => g.Last());
        }

        public bool TryGet(Type source, Type destination, out TypeMap map)
        {
            return store.TryGetValue((source, destination), out map)
                || TryGetOpenGeneric(source, destination, out map);
        }

        //This needs to be configurable/extensible regarding open types attempted
        private bool TryGetOpenGeneric(Type source, Type destination, out TypeMap map)
        {
            map = null;

            //I'm really proud of this bytewise OR
            return TryOpen(source, out var src) | TryOpen(destination, out var dst)
                   && store.TryGetValue((src ?? source, dst ?? destination), out map);
        }

        private static bool TryOpen(Type type, out Type open)
        {
            if (type.IsGenericType)
            {
                open = type.GetGenericTypeDefinition();
                return true;
            }
            if (type.IsArray)
            {
                open = typeof(Array);
                return true;
            }

            open = null;
            return false;
        }
    }

    public class TupleTypeMapDictionary : TypeMapDictionary
    {
        private readonly IReadOnlyDictionary<Tuple<Type, Type>, TypeMap> store;

        public TupleTypeMapDictionary(IEnumerable<TypeMap> maps)
        {
            store = maps.ToDictionary(m => Tuple.Create(m.Source, m.Destination), new TypeTupleComparer());
        }

        public bool TryGet(Type source, Type destination, out TypeMap map)
        {
            return store.TryGetValue(Tuple.Create(source, destination), out map);
        }

        private class TypeTupleComparer : IEqualityComparer<Tuple<Type, Type>>
        {
            public bool Equals(Tuple<Type, Type> x, Tuple<Type, Type> y)
            {
                return x.Item1 == y.Item1 && x.Item2 == y.Item2;
            }

            public int GetHashCode(Tuple<Type, Type> obj)
            {
                return obj.Item1.GetHashCode() ^ obj.Item2.GetHashCode();
            }
        }
    }
}