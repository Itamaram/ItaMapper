using ItaMapper.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ItaMapper.Exceptions;

namespace ItaMapper
{
    public class TypeMapConfig<A, B>
    {
        private readonly HashSet<string> targets;

        public TypeMapConfig()
        {
            Actions = new List<MappingAction<A, B>>();
            targets = new HashSet<string>();
        }

        private TypeMapConfig(IEnumerable<MappingAction<A, B>> actions, IEnumerable<string> targets)
        {
            Actions = actions.ToList();
            this.targets = new HashSet<string>(targets);
        }

        public TypeMapConfig<A, B> AddAction(MappingAction<A, B> action)
        {
            return new TypeMapConfig<A, B>(Actions.Append(action), targets.AppendIfNotNull(action.Target));
        }

        public IEnumerable<MappingAction<A, B>> Actions { get; }

        public IEnumerable<string> Targets => targets;

        public bool Targeting(string target) => targets.Contains(target);

        //implicit convert to TypeMap?
    }

    public static class TypeMapConfigExtensions
    {
        //should throw a better error for unmappable properties
        public static TypeMapConfig<A, B> MapRemainingProperties<A, B>(this TypeMapConfig<A, B> config)
        {
            return typeof(B).GetProperties()
                .Where(p => !config.Targeting(p.Name))
                .Aggregate(config, (c, p) => c.Map(p).From(p.Name));
        }

        public static TypeMapConfig<A, B> AssertAllPropertiesAreMapped<A, B>(this TypeMapConfig<A, B> config)
        {
            typeof(B).GetProperties()
                .FirstOrDefault(p => !config.Targeting(p.Name))
                ?.Do(p => throw new UnmappedPropertyException<A, B>(p));

            return config;
        }

        public static TypeMap ToMap<A, B>(this TypeMapConfig<A, B> config) => new ActionAggregateTypeMap<A, B>(config);

        public static TypeMapConfig<A, B> Before<A, B>(this TypeMapConfig<A, B> config, Action<A, B, MappingContext> before)
        {
            return config.AddAction(new TargetFreeAction<A, B>(before, MappingPhase.BeforeMapping));
        }

        public static TypeMapConfig<A, B> After<A, B>(this TypeMapConfig<A, B> config, Action<A, B, MappingContext> after)
        {
            return config.AddAction(new TargetFreeAction<A, B>(after, MappingPhase.AfterMapping));
        }

        public static TypeMapContext<A, B> Map<A, B>(this TypeMapConfig<A, B> config, Expression<Func<B, object>> selector)
        {
            //todo exceptions. Just a few.
            return config.Map(selector.GetMemberExpression().GetPropertyInfo());
        }

        public static TypeMapContext<A, B> Map<A, B>(this TypeMapConfig<A, B> config, PropertyInfo property)
        {
            return new TypeMapContext<A, B>(config, property);
        }
    }

    public class UnmappedPropertyException<A, B> : Exception
    {
        public UnmappedPropertyException(PropertyInfo pi)
            : base($"TypeMap {typeof(A)} -> {typeof(B)} does not map to destination property '{pi.Name}'")
        {
        }
    }

    public class TypeMapContext<A, B>
    {
        private readonly TypeMapConfig<A, B> config;
        private readonly PropertyInfo property;

        public TypeMapContext(TypeMapConfig<A, B> config, PropertyInfo property)
        {
            this.config = config;
            this.property = property;
        }

        public TypeMapConfig<A, B> Action(Func<PropertyInfo, MappingAction<A, B>> action)
        {
            return config.AddAction(action(property));
        }

        // todo rename this. Rething this?
        public TypeMapConfig<A, B> PropertyResolver(Func<PropertyInfo, ValueResolver<A, B>> factory)
        {
            return factory(property).Pipe(r => Resolver(_ => r));
        }

        public TypeMapConfig<A, B> Resolver(Func<MappingContext, ValueResolver<A, B>> factory)
        {
            return config.AddAction(new ResolverMappingAction<A, B>(property, factory));
        }

        public TypeMapConfig<A, B> Resolver(ValueResolver<A, B> resolver)
        {
            return Resolver(_ => resolver);
        }

        public TypeMapConfig<A, B> Resolver(Type resolver)
        {
            if (!typeof(ValueResolver<A, B>).IsAssignableFrom(resolver))
                throw new TypeIsNotResolverException<A, B>(resolver);

            return Resolver(args => (ValueResolver<A, B>)args.Instantiator.Create(resolver));
        }

        //todo can I pull this out to an extension?
        public TypeMapConfig<A, B> Resolver<C>() where C : ValueResolver<A, B> => Resolver(typeof(C));
    }

    public static class TypeMapContextExtensions
    {
        public static TypeMapConfig<A, B> Using<A, B, C>(this TypeMapContext<A, B> context, Func<PropertyMapArguments<A, B>, C> map)
        {
            return context.Resolver(new InlineResolver<A, B, C>(map));
        }

        public static TypeMapConfig<A, B> From<A, B, C>(this TypeMapContext<A, B> context, Func<A, C> map)
        {
            return context.Resolver(new InlineResolver<A, B, C>(args => args.Source.Pipe(map)));
        }

        public static TypeMapConfig<A, B> From<A, B>(this TypeMapContext<A, B> context, string name)
        {
            return context.Resolver(new FromPropertyResolver<A, B>(name));
        }

        public static TypeMapConfig<A, B> ToSelf<A, B>(this TypeMapContext<A, B> context)
        {
            return context.PropertyResolver(pi => new FromPropertyResolver<A, B>(pi));
        }

        public static TypeMapConfig<A, B> Ignore<A, B>(this TypeMapContext<A, B> context)
        {
            return context.Action(e => new NoopAction<A, B>(e.Name));
        }
    }

    public interface ValueResolver<A, B>
    {
        object Resolve(PropertyMapArguments<A, B> args);

        //todo pretend this is not a smell
        Type MemberType { get; }
    }

    public abstract class ValueResolver<A, B, C> : ValueResolver<A, B>
    {
        public object Resolve(PropertyMapArguments<A, B> args)
        {
            return ResolveValue(args);
        }

        protected abstract C ResolveValue(PropertyMapArguments<A, B> args);

        public Type MemberType { get; } = typeof(C);
    }

    public class InlineResolver<A, B, C> : ValueResolver<A, B, C>
    {
        private readonly Func<PropertyMapArguments<A, B>, C> map;

        public InlineResolver(Func<PropertyMapArguments<A, B>, C> map) => this.map = map;

        protected override C ResolveValue(PropertyMapArguments<A, B> args) => map(args);
    }

    public class FromPropertyResolver<A, B> : ValueResolver<A, B>
    {
        //todo this can be a more specific getter
        private readonly Func<object, object> getter;

        public FromPropertyResolver(PropertyInfo property)
        {
            MemberType = property.PropertyType;
            getter = new ExpressionBuilder().Getter(typeof(A), property.Name);
        }

        public FromPropertyResolver(string name)
        {
            MemberType = typeof(A).GetProperty(name)?.PropertyType ?? throw new NoDirectMapTargetException(typeof(A), name);
            getter = new ExpressionBuilder().Getter(typeof(A), name);
        }

        public object Resolve(PropertyMapArguments<A, B> args) => getter(args.Source);

        public Type MemberType { get; }
    }

    public class FromPropertyResolver<A, B, C> : ValueResolver<A, B, C>
    {
        private readonly Func<B, C> expression;

        public FromPropertyResolver(Func<B, C> expression)
        {
            this.expression = expression;
        }

        protected override C ResolveValue(PropertyMapArguments<A, B> args) => expression(args.Destination);
    }
}