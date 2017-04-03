using System;
using System.Collections.Generic;
using System.Linq;

namespace IttyMapper
{
    public interface TypeMap
    {
        Type Source { get; }
        Type Destination { get; }
        object Map(object source, ObjectInstantiator instantiator, Mapper mapper, Context context);
    }

    public abstract class TypeMap<TSource, TDestination> : TypeMap
    {
        public Type Source { get; } = typeof(TSource);

        public Type Destination { get; } = typeof(TDestination);

        public object Map(object source, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
            return Map((TSource)source, instantiator, mapper, context);
        }

        protected abstract TDestination Map(TSource source, ObjectInstantiator instantiator, Mapper mapper, Context context);
    }

    public class PassthroughMap<A> : TypeMap
    {
        public Type Source { get; } = typeof(A);

        public Type Destination { get; } = typeof(A);

        public object Map(object source, ObjectInstantiator instantiator, Mapper mapper, Context context) => source;
    }

    public class ActionAggregateTypeMap<TSource, TDestination> : TypeMap<TSource, TDestination>
    {
        private readonly IReadOnlyList<MappingAction<TSource, TDestination>> actions;

        public ActionAggregateTypeMap(TypeMapConfig<TSource, TDestination> config)
        {
            actions = config.Actions.OrderBy(a => a.Priority).ToList();
        }

        protected override TDestination Map(TSource source, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
            var dst = (TDestination)instantiator.Create(Destination);

            foreach (var action in actions)
                action.Map(source, dst, instantiator, mapper, context);

            return dst;
        }
    }
}