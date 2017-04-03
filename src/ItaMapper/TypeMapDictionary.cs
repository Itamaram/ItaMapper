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
            store = maps.ToDictionary(m => (m.Source, m.Destination));
        }

        public bool TryGet(Type source, Type destination, out TypeMap map)
        {
            return store.TryGetValue((source, destination), out map);
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