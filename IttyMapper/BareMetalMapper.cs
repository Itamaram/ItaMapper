using System;
using System.Collections.Generic;
using IttyMapper.Exceptions;

namespace IttyMapper
{
    public class BareMetalMapper : Mapper
    {
        private readonly ObjectInstantiator instantiator;
        private readonly TypeMapDictionary maps;
        
        public BareMetalMapper(IEnumerable<TypeMap> maps, ObjectInstantiator instantiator)
        {
            this.instantiator = instantiator;
            this.maps = new ValueTupleTypeMapDictionary(maps);
        }

        public object Map(object src, Type source, Type destination, Context context)
        {
            if (maps.TryGet(source, destination, out var map))
                return map.Map(src, instantiator, this, context);

            throw new UnconfiguredMapException(source, destination);
        }
    }
}