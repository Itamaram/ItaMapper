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

    public class GenericTypeMapTypeProvider : TypeMapTypeProvider
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

    public interface TypeMapTypeProvider
    {
        Type Create(Type source, Type destination);
    }

    public class IdentityTypeMapTypeProvider : TypeMapTypeProvider
    {
        private readonly Type map;

        public IdentityTypeMapTypeProvider(Type map)
        {
            if (!typeof(TypeMap).IsAssignableFrom(map))
                throw new Exception($"{map} is not assignable from TypeMap");

            this.map = map;
        }

        public Type Create(Type source, Type destination) => map;
    }

    public class FactoryTypeMap : TypeMap
    {
        private readonly TypeMapTypeProvider provider;

        public FactoryTypeMap(Type source, Type destination, TypeMapTypeProvider provider)
        {
            this.provider = provider;
            Source = source;
            Destination = destination;
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

    public class PassthroughMap : TypeMap
    {
        public PassthroughMap(Type type)
        {
            Source = type;
            Destination = type;
        }

        public Type Source { get; }

        public Type Destination { get; }

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

    public static class CreateTypeMap
    {
        public static CreateTypeMapFrom From(Type source) => new CreateTypeMapFrom(source);

        public static CreateTypeMapFrom<A> From<A>() => new CreateTypeMapFrom<A>();
    }

    public class CreateTypeMapFrom
    {
        public CreateTypeMapFrom(Type source)
        {
            Source = source;
        }

        public Type Source { get; }

        public CreateTypeMapTo To(Type destination) => new CreateTypeMapTo(Source, destination);
    }

    public class CreateTypeMapFrom<A> : CreateTypeMapFrom
    {
        public CreateTypeMapFrom() : base(typeof(A)) { }

        public CreateTypeMapTo<A, B> To<B>() => new CreateTypeMapTo<A, B>();
    }

    public class CreateTypeMapTo
    {
        public CreateTypeMapTo(Type source, Type destination)
        {
            Source = source;
            Destination = destination;
        }

        public Type Source { get; }
        public Type Destination { get; }
    }

    public class CreateTypeMapTo<A, B> : CreateTypeMapTo
    {
        public CreateTypeMapTo() : base(typeof(A), typeof(B)) { }
    }

    public static class CreateTypeMapExtensions
    {
        public static TypeMap Using<A, B>(this CreateTypeMapTo<A, B> tuple, Func<A, MappingContext, B> map)
        {
            return new FuncTypeMap<A, B>(map);
        }

        public static TypeMap Using(this CreateTypeMapTo tuple, Type factory)
        {
            return new FactoryTypeMap(tuple.Source, tuple.Destination, GetProvider(factory));
        }

        public static TypeMap Using(this CreateTypeMapTo tuple, Type primary, Type secondary)
        {
            return new FactoryTypeMap(tuple.Source, tuple.Destination, new GenericTypeMapTypeProvider(primary, secondary));
        }

        public static TypeMapConfig<A, B> Propertywise<A, B>(this CreateTypeMapTo<A, B> tuple)
        {
            return new TypeMapConfig<A, B>();
        }

        private static TypeMapTypeProvider GetProvider(Type provider)
        {
            if (provider.IsGenericTypeDefinition)
                return new GenericTypeMapTypeProvider(provider);

            return new IdentityTypeMapTypeProvider(provider);
        }

        public static TypeMap ToSelf(this CreateTypeMapFrom tuple)
        {
            return new PassthroughMap(tuple.Source);
        }
    }
}