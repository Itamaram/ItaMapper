using System;
using System.Collections.Generic;
using System.Linq;
using ItaMapper.Extensions;

namespace ItaMapper
{
    public class ItaMapper : BareBoneMapper
    {
        public ItaMapper(IEnumerable<TypeMap> maps) : this(maps, new ActivatorInstantiator())
        {
        }

        public ItaMapper(IEnumerable<TypeMap> maps, ObjectInstantiator instantiator)
            : base(defaults.Concat(maps), instantiator)
        {
        }

        private static readonly IEnumerable<TypeMap> defaults;

        static ItaMapper()
        {
            defaults = MapToEnumerable(typeof(List<>), typeof(ToListTypeMapper<,>))
                .Concat(MapToEnumerable(typeof(GenericArray), typeof(ToArrayTypeMapper<,>)))
                .Append(CreateTypeMap.From<string>().ToSelf())
                //todo more primitives.
                .ToArray();
        }

        private static IEnumerable<TypeMap> MapToEnumerable(Type result, Type mapper)
        {
            return new[]
            {
                typeof(IEnumerable<>),
                typeof(ICollection<>),
                typeof(IList<>),
                typeof(GenericArray),
                typeof(List<>)
            }.Select(t => CreateTypeMap.From(t).To(result).Using(mapper));
        }
    }

    public abstract class EnumerableMapper<A, B, T, U> : TypeMap<A, B> where A : IEnumerable<T> where B : IEnumerable<U>
    {
        protected override B Map(A source, MappingContext context)
        {
            return source.Select(item => context.Mapper.Map<T, U>(item, context.State)).Pipe(CreateEnumerable);
        }

        protected abstract B CreateEnumerable(IEnumerable<U> items);
    }

    public class ToListTypeMapper<A, B> : EnumerableMapper<IEnumerable<A>, List<B>, A, B>
    {
        protected override List<B> CreateEnumerable(IEnumerable<B> items) => items.ToList();
    }

    public class ToArrayTypeMapper<A, B> : EnumerableMapper<IEnumerable<A>, B[], A, B>
    {
        protected override B[] CreateEnumerable(IEnumerable<B> items) => items.ToArray();
    }
}