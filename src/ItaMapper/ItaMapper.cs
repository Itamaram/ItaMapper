using System;
using System.Collections.Generic;
using System.Linq;

namespace ItaMapper
{
    public class ItaMapper : BareBoneMapper
    {
        public ItaMapper(IEnumerable<TypeMap> maps) : this(maps, new ActivatorInstantiator())
        {
        }

        public ItaMapper(IEnumerable<TypeMap> maps, ObjectInstantiator instantiator)
            : base(maps.Concat(defaults), instantiator)
        {
        }

        private static readonly IEnumerable<TypeMap> defaults = new TypeMap[]
        {
            new PassthroughMap<string>(),
            //todo more primitives.
            new GenericFactoryTypeMap(typeof(List<>), typeof(List<>), typeof(ToListTypeMapper<>), typeof(ToListTypeMapper<,>)), 
        };
    }

    //This will create a new list even when mapping from a list to a list. This is a feature.
    public class ToListTypeMapper<A> : ToListTypeMapper<A, A> { }

    public class ToListTypeMapper<A, B> : FuncTypeMap<IEnumerable<A>, List<B>>
    {
        public ToListTypeMapper() : base(Func) { }

        private static List<B> Func(IEnumerable<A> items, MappingContext context)
        {
            return items.Select(i => context.Mapper.Map<A, B>(i, context.State)).ToList();
        }
    }
}