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

        private static readonly IEnumerable<TypeMap> defaults = new[]
        {
            new PassthroughMap<string>(),
            //todo more primitives.
        };
    }
}