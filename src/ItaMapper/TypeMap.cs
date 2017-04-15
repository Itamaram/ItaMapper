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

    public static class GenericTypeFactory
    {
        public static bool IsOpen(Type type)
        {
            return type == typeof(Array) || type.IsGenericTypeDefinition;
        }

        public static OpenGenericType GetOpen(Type type)
        {
            if (type == typeof(Array))
                return new OpenArray();

            return new OpenType(type);
        }

        public static ClosedGenericType GetClosed(Type type)
        {
            if (type.IsArray)
                return new ClosedArray(type);

            return new ClosedType(type);
        }
    }

    public static class OpenGenericTypeExtensions
    {
        public static Type MakeGenericType(this OpenGenericType open, params ClosedGenericType[] closed)
        {
            return open.MakeGenericType(closed.SelectMany(c => c.GenericArguments).ToArray());
        }

        public static Type MakeGenericType(this OpenGenericType open, ClosedGenericType closed)
        {
            return open.MakeGenericType(closed.GenericArguments);
        }
    }

    public interface OpenGenericType
    {
        int ArgumentsCount { get; }
        Type MakeGenericType(Type[] args);
    }

    public class OpenArray : OpenGenericType
    {
        public int ArgumentsCount { get; } = 1;

        public Type MakeGenericType(Type[] args)
        {
            if (args.Length != 1)
                throw new Exception(/*todo*/);

            return Array.CreateInstance(args[0], 0).GetType();
        }
    }

    //This actually handles nongenerics too. Whoops.
    public class OpenType : OpenGenericType
    {
        private readonly Type type;

        public OpenType(Type type)
        {
            this.type = type;
            ArgumentsCount = type.IsGenericTypeDefinition ? type.GetGenericArguments().Length : 0;
        }

        public int ArgumentsCount { get; }

        public Type MakeGenericType(Type[] args)
        {
            return ArgumentsCount == 0 ? type : type.MakeGenericType(args);
        }
    }

    public interface ClosedGenericType
    {
        Type[] GenericArguments { get; }
    }

    public class ClosedType : ClosedGenericType
    {
        public ClosedType(Type type)
        {
            GenericArguments = type.GenericTypeArguments;
        }

        public Type[] GenericArguments { get; }
    }

    public class ClosedArray : ClosedGenericType
    {
        public ClosedArray(Type type)
        {
            GenericArguments = new[] { type.GetElementType() };
        }

        public Type[] GenericArguments { get; }
    }

    internal static class GenericTypeDefinitionExtensions
    {
        public static bool TotalArgCountMatch(this OpenGenericType generic, params ClosedGenericType[] types)
        {
            return generic.ArgumentsCount == types.Sum(t => t.GenericArguments.Length);
        }
    }

    public class GenericTypeMapTypeProvider : TypeMapTypeProvider
    {
        private readonly OpenGenericType primary;
        private readonly OpenGenericType fallback;

        public GenericTypeMapTypeProvider(Type primary)
        {
            this.primary = GenericTypeFactory.GetOpen(primary);
        }

        public GenericTypeMapTypeProvider(Type primary, Type fallback)
        {
            this.primary = GenericTypeFactory.GetOpen(primary);
            this.fallback = GenericTypeFactory.GetOpen(fallback);

            //todo ensure types are generic with open params with separate having more variables than overlap
        }

        public Type Create(Type source, Type destination)
        {
            var src = GenericTypeFactory.GetClosed(source);
            var dst = GenericTypeFactory.GetClosed(destination);
            if (primary.TotalArgCountMatch(src, dst))
                return primary.MakeGenericType(src, dst);

            if (fallback == null)
            {
                if (!src.GenericArguments.SequenceEqual(dst.GenericArguments))
                    throw new Exception();

                if (!primary.TotalArgCountMatch(src))
                    throw new Exception();

                return primary.MakeGenericType(src);
            }

            if (src.GenericArguments.SequenceEqual(dst.GenericArguments) && primary.TotalArgCountMatch(src))
                return primary.MakeGenericType(src);

            if (fallback.TotalArgCountMatch(src, dst))
                return fallback.MakeGenericType(src, dst);

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
            if (GenericTypeFactory.IsOpen(provider))
                return new GenericTypeMapTypeProvider(provider);

            return new IdentityTypeMapTypeProvider(provider);
        }

        public static TypeMap ToSelf(this CreateTypeMapFrom tuple)
        {
            return new PassthroughMap(tuple.Source);
        }
    }
}