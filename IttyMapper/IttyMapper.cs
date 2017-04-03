using System.Collections.Generic;
using System.Linq;

namespace IttyMapper
{
    public class IttyMapper : BareMetalMapper
    {
        public IttyMapper(IEnumerable<TypeMap> maps) : this(maps, new ActivatorInstantiator())
        {
        }

        public IttyMapper(IEnumerable<TypeMap> maps, ObjectInstantiator instantiator)
            : base(maps.Concat(defaults), instantiator)
        {
        }

        private static readonly IEnumerable<TypeMap> defaults = new[]
        {
            new PassthroughMap<string>(),
        };
    }
}