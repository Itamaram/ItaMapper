using System;
using System.Collections.Generic;
using System.Linq;
using ItaMapper.Extensions;

namespace ItaMapper
{
    public interface TypeMap
    {
        Type Source { get; }
        Type Destination { get; }
        object Map(object source, MappingContext context);
    }

    public abstract class TypeMap<TSource, TDestination> : TypeMap
    {
        public Type Source { get; } = typeof(TSource);

        public Type Destination { get; } = typeof(TDestination);

        public object Map(object source, MappingContext context)
        {
            return Map((TSource)source, context);
        }

        protected abstract TDestination Map(TSource source, MappingContext context);
    }

    public class FuncTypeMap<TSource, TDestination> : TypeMap<TSource, TDestination>
    {
        private readonly Func<TSource, MappingContext, TDestination> map;

        public FuncTypeMap(Func<TSource, MappingContext, TDestination> map)
        {
            this.map = map;
        }

        protected override TDestination Map(TSource source, MappingContext context) => map(source, context);
    }

    public class FactoryTypeMap : TypeMap
    {
        private readonly Type map;

        public FactoryTypeMap(Type source, Type destination, Type map)
        {
            Source = source;
            Destination = destination;

            if (!typeof(TypeMap).IsAssignableFrom(map))
                throw new Exception($"{map} is not assignable from TypeMap");

            this.map = map;
        }

        public Type Source { get; }
        public Type Destination { get; }

        public object Map(object source, MappingContext context)
        {
            var type = map.IsGenericTypeDefinition ? MakeGenericType(context.Source, context.Destination) : map;
            return context.Instantiator.Create(type).Cast<TypeMap>().Map(source, context);
        }

        private Type MakeGenericType(Type source, Type destination)
        {
            var count = map.GetGenericArguments().Length;

            if (source.GenericTypeArguments.Length + destination.GenericTypeArguments.Length == count)
                return MakeGenericType(map, source.GenericTypeArguments.Concat(destination.GenericTypeArguments).ToArray());

            if (source.GenericTypeArguments.SequenceEqual(destination.GenericTypeArguments) && source.GenericTypeArguments.Length == count)
                return MakeGenericType(map, source.GenericTypeArguments);

            throw new Exception($"Couldn't make type {map} generic using [{string.Join<Type>(", ", source.GenericTypeArguments)}] and [{string.Join<Type>(", ", destination.GenericTypeArguments)}]");
        }

        private static Type MakeGenericType(Type generic, Type[] args)
        {
            try
            {
                return generic.MakeGenericType(args);
            }
            catch (ArgumentException e)
            {
                //todo create exception
                throw new Exception($"Couldn't make type {generic} generic using [{string.Join<Type>(", ", args)}]", e);
            }
        }
    }

    public class PassthroughMap<A> : TypeMap
    {
        public Type Source { get; } = typeof(A);

        public Type Destination { get; } = typeof(A);

        public object Map(object source, MappingContext context) => source;
    }

    public class ActionAggregateTypeMap<TSource, TDestination> : TypeMap<TSource, TDestination>
    {
        private readonly IReadOnlyList<MappingAction<TSource, TDestination>> actions;

        public ActionAggregateTypeMap(TypeMapConfig<TSource, TDestination> config)
        {
            actions = config.Actions.OrderBy(a => a.Priority).ToList();
        }

        protected override TDestination Map(TSource source, MappingContext context)
        {
            var dst = (TDestination)context.Instantiator.Create(Destination);

            foreach (var action in actions)
                action.Map(source, dst, context);

            return dst;
        }
    }
}