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

    public class GenericTypeDefinition
    {
        private readonly Type open;

        public GenericTypeDefinition(Type open)
        {
            //ensure actually open
            this.open = open;
            GenericArgumentsCount = open.GetGenericArguments().Length;
        }

        public int GenericArgumentsCount { get; }

        public Type MakeGeneric(Type[] types)
        {
            return open.MakeGenericType(types);
        }

        public bool TotalArgCountMatch(params Type[] types)
        {
            return GenericArgumentsCount == types.Sum(t => t.GenericTypeArguments.Length);
        }
    }

    public class GenericTypeMapTypeProvider
    {
        private readonly GenericTypeDefinition primary;
        private readonly GenericTypeDefinition fallback;

        public GenericTypeMapTypeProvider(Type primary)
        {
            this.primary = new GenericTypeDefinition(primary);
        }

        public GenericTypeMapTypeProvider(Type primary, Type fallback)
        {
            this.primary = new GenericTypeDefinition(primary);
            this.fallback = new GenericTypeDefinition(fallback);

            //ensure types are generic with open params with separate having more variables than overlap
        }

        public Type Create(Type source, Type destination)
        {
            if (primary.TotalArgCountMatch(source, destination))
                return primary.MakeGeneric(source.GenericTypeArguments.Concat(destination.GenericTypeArguments).ToArray());

            if (fallback == null)
            {
                if (!source.GenericTypeArguments.SequenceEqual(destination.GenericTypeArguments))
                    throw new Exception();

                if (!primary.TotalArgCountMatch(source))
                    throw new Exception();

                return primary.MakeGeneric(source.GenericTypeArguments);
            }

            if (source.GenericTypeArguments.SequenceEqual(destination.GenericTypeArguments) && primary.TotalArgCountMatch(source))
                return primary.MakeGeneric(source.GenericTypeArguments);

            if (fallback.TotalArgCountMatch(source, destination))
                return fallback.MakeGeneric(source.GenericTypeArguments.Concat(destination.GenericTypeArguments).ToArray());

            throw new Exception();
        }
    }

    public class GenericFactoryTypeMap : TypeMap
    {
        private readonly GenericTypeMapTypeProvider provider;

        public GenericFactoryTypeMap(Type source, Type destination, Type factory)
        {
            Source = source;
            Destination = destination;
            provider = new GenericTypeMapTypeProvider(factory);
        }

        public GenericFactoryTypeMap(Type source, Type destination, Type factory, Type fallback)
        {
            Source = source;
            Destination = destination;
            provider = new GenericTypeMapTypeProvider(factory, fallback);
        }

        public Type Source { get; }

        public Type Destination { get; }

        public object Map(object source, MappingContext context)
        {
            return provider.Create(context.Source, context.Destination)
                .Pipe(context.Instantiator.Create)
                .Cast<TypeMap>()
                .Map(source, context);
        }
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
            return context.Instantiator.Create(map).Cast<TypeMap>().Map(source, context);
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