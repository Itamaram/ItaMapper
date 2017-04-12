using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using ItaMapper.Extensions;

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
    }

    public static class TypeMapConfigExtensions
    {
        //should throw a better error for unmappable properties
        public static TypeMapConfig<A, B> MapRemainingProperties<A, B>(this TypeMapConfig<A, B> config)
        {
            return typeof(B).GetProperties()
                .Where(p => !config.Targeting(p.Name))
                .Aggregate(config, (c, p) => c.AddAction(new DirectPropertyMap<A, B>(p.Name)));
        }

        public static TypeMapConfig<A, B> Map<A, B, C>(this TypeMapConfig<A, B> config, Expression<Func<B, object>> selector, Func<PropertyMapArguments<A, B>, C> map)
        {
            return config.AddAction(new InlinePropertyMap<A, B, C>(selector, map));
        }

        public static TypeMapConfig<A, B> Ignore<A, B>(this TypeMapConfig<A, B> config,
            Expression<Func<B, object>> selector)
        {
            return config.AddAction(new NoopAction<A, B>(selector));
        }

        public static TypeMap ToMap<A, B>(this TypeMapConfig<A, B> config) => new ActionAggregateTypeMap<A, B>(config);

        public static TypeMapConfig<A, B> Before<A, B>(this TypeMapConfig<A, B> config, Action<A, B, ObjectInstantiator, Mapper, Context> before)
        {
            return config.AddAction(new TargetFreeAction<A, B>(before, MappingPhase.BeforeMapping));
        }

        public static TypeMapConfig<A, B> After<A, B>(this TypeMapConfig<A, B> config, Action<A, B, ObjectInstantiator, Mapper, Context> after)
        {
            return config.AddAction(new TargetFreeAction<A, B>(after, MappingPhase.AfterMapping));
        }

        public static TypeMapContext<A, B> Map<A, B>(this TypeMapConfig<A, B> config, Expression<Func<B, object>> selector)
        {
            return new TypeMapContext<A, B>(config, selector);
        }
    }

    public class TypeMapContext<A, B>
    {
        private readonly TypeMapConfig<A, B> config;
        private readonly Expression<Func<B, object>> selector;

        public TypeMapContext(TypeMapConfig<A, B> config, Expression<Func<B, object>> selector)
        {
            this.config = config;
            this.selector = selector;
        }

        public TypeMapConfig<A, B> Apply(Func<TypeMapConfig<A, B>, Expression<Func<B, object>>, TypeMapConfig<A, B>> apply)
        {
            return apply(config, selector);
        }
    }

    public static class TypeMapContextExtensions
    {
        public static TypeMapConfig<A, B> From<A, B, C>(this TypeMapContext<A, B> context, Func<PropertyMapArguments<A, B>, C> map)
        {
            return context.Apply((config, selector) => config.Map(selector, map));
        }

        public static TypeMapConfig<A, B> FromSource<A, B, C>(this TypeMapContext<A, B> context, Func<A, C> map)
        {
            return context.Apply((config, selector) => config.Map(selector, args => args.Source.Pipe(map)));
        }


    }
}