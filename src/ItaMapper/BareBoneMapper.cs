using System;
using System.Collections.Generic;
using ItaMapper.Exceptions;

namespace ItaMapper
{
    public class BareBoneMapper : Mapper
    {
        private readonly ObjectInstantiator instantiator;
        private readonly TypeMapDictionary maps;

        public BareBoneMapper(IEnumerable<TypeMap> maps, ObjectInstantiator instantiator)
        {
            this.instantiator = instantiator;
            this.maps = new ValueTupleTypeMapDictionary(maps);
        }

        public object Map(object src, Type source, Type destination, MappingState state)
        {
            if (maps.TryGet(source, destination, out var map))
                return map.Map(src, new MappingContext(source, destination, this, instantiator, state));

            throw new UnconfiguredMapException(source, destination);
        }
    }
}