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
        private static readonly IEnumerable<OpenGenericTypeHandler> ohandlers = new OpenGenericTypeHandler[]
        {
            new OpenArrayHandler(),
            new OpenTypeHandler(),
            new NotOpenTypeHandler()
        };

        private static readonly IEnumerable<ClosedGenericTypeHandler> bings = new ClosedGenericTypeHandler[]
        {
            new ClosedArrayHandler(),
            new ClosedTypeHandler()
        };

        public static OpenGenericType Open(Type type)
        {
            return ohandlers.First(h => h.Handles(type)).Create(type);
        }

        public static ClosedGenericType Closed(Type type)
        {
            return bings.First(h => h.Handles(type)).Create(type);
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

    public interface OpenGenericTypeHandler
    {
        bool Handles(Type type);
        OpenGenericType Create(Type type);
    }

    public interface OpenGenericType
    {
        int ArgumentsCount { get; }
        Type MakeGenericType(Type[] args);
    }

    public class OpenArrayHandler : OpenGenericTypeHandler, OpenGenericType
    {
        public bool Handles(Type type) => type == typeof(Array);

        public OpenGenericType Create(Type type) => this;

        public int ArgumentsCount { get; } = 1;
        public Type MakeGenericType(Type[] args)
        {
            if (args.Length != 1)
                throw new Exception(/*todo*/);

            return Array.CreateInstance(args[0], 0).GetType();
        }
    }

    public class OpenTypeHandler : OpenGenericTypeHandler
    {
        public bool Handles(Type type) => type.IsGenericTypeDefinition;

        public OpenGenericType Create(Type type) => new OpenType(type);
    }

    public class OpenType : OpenGenericType
    {
        private readonly Type type;

        public OpenType(Type type)
        {
            this.type = type;
            ArgumentsCount = type.GetGenericArguments().Length;
        }

        public int ArgumentsCount { get; }

        public Type MakeGenericType(Type[] args) => type.MakeGenericType(args);
    }

    public class NotOpenTypeHandler : OpenGenericTypeHandler
    {
        public bool Handles(Type type) => true;

        public OpenGenericType Create(Type type) => new NotOpenType(type);
    }

    public class NotOpenType : OpenGenericType
    {
        private readonly Type type;

        public NotOpenType(Type type)
        {
            this.type = type;
        }

        public int ArgumentsCount { get; } = 0;

        public Type MakeGenericType(Type[] args) => type;
    }

    public interface ClosedGenericTypeHandler
    {
        bool Handles(Type type);
        ClosedGenericType Create(Type type);
    }

    public interface ClosedGenericType
    {
        Type[] GenericArguments { get; }
    }

    public class ClosedTypeHandler : ClosedGenericTypeHandler
    {
        public bool Handles(Type type) => true;

        public ClosedGenericType Create(Type type) => new ClosedType(type);
    }

    public class ClosedType : ClosedGenericType
    {
        public ClosedType(Type type)
        {
            GenericArguments = type.GenericTypeArguments;
        }

        public Type[] GenericArguments { get; }
    }

    public class ClosedArrayHandler : ClosedGenericTypeHandler
    {
        public bool Handles(Type type) => type.IsArray;

        public ClosedGenericType Create(Type type) => new ClosedArray(type);
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

    public class GenericTypeOverlap : TypeMapTypeProvider
    {
        private readonly OpenGenericType generic;

        public GenericTypeOverlap(OpenGenericType generic)
        {
            this.generic = generic;
        }

        public Type Create(Type source, Type destination)
        {
            var src = GenericTypeFactory.Closed(source);
            var dst = GenericTypeFactory.Closed(destination);

            if (src.GenericArguments.SequenceEqual(dst.GenericArguments))
                return generic.MakeGenericType(src);

            throw new Exception(/*todo*/);
        }
    }

    public class GenericTypeConcat : TypeMapTypeProvider
    {
        private readonly OpenGenericType generic;

        public GenericTypeConcat(OpenGenericType generic)
        {
            this.generic = generic;
        }

        public Type Create(Type source, Type destination)
        {
            return generic.MakeGenericType(GenericTypeFactory.Closed(source), GenericTypeFactory.Closed(destination));
        }
    }

    public class GenericTypeMapTypeProvider : TypeMapTypeProvider
    {
        private readonly TypeMapTypeProvider provider;

        public GenericTypeMapTypeProvider(Type source, Type destination, Type provider)
            : this(source, destination, GenericTypeFactory.Open(provider)) { }

        public GenericTypeMapTypeProvider(Type source, Type destination, OpenGenericType provider)
        {
            var src = GenericTypeFactory.Open(source);
            var dst = GenericTypeFactory.Open(destination);

            if (src.ArgumentsCount == dst.ArgumentsCount && src.ArgumentsCount == provider.ArgumentsCount)
            {
                this.provider = new GenericTypeOverlap(provider);
            }
            else if (provider.ArgumentsCount == src.ArgumentsCount + dst.ArgumentsCount)
            {
                this.provider = new GenericTypeConcat(provider);
            }
            else
            {
                throw new Exception(/*todo*/);
            }
        }

        public Type Create(Type source, Type destination) => provider.Create(source, destination);
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

        //todo generic/nongeneric split should happen here
        //todo arg count verification should happen now, split betwen single or double is obvs runtime
        public static TypeMap Using(this CreateTypeMapTo tuple, Type factory)
        {
            var open = GenericTypeFactory.Open(factory);

            TypeMapTypeProvider provider;
            if (open.ArgumentsCount == 0)
                provider = new IdentityTypeMapTypeProvider(factory);
            else
                provider = new GenericTypeMapTypeProvider(tuple.Source, tuple.Destination, open);

            return new FactoryTypeMap(tuple.Source, tuple.Destination, provider);
        }

        public static TypeMapConfig<A, B> Propertywise<A, B>(this CreateTypeMapTo<A, B> tuple)
        {
            return new TypeMapConfig<A, B>();
        }

        public static TypeMap ToSelf(this CreateTypeMapFrom tuple)
        {
            return new PassthroughMap(tuple.Source);
        }
    }
}