using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
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

        public static TypeMapConfig<A, B> Map<A, B>(this TypeMapConfig<A, B> config, Expression<Func<B, object>> selector, Func<PropertyMapArguments<A,B>, object> map)
        {
            return config.AddAction(new InlinePropertyMap<A, B>(selector, map));
        }

        public static TypeMapConfig<A, B> Ignore<A, B>(this TypeMapConfig<A, B> config,
            Expression<Func<B, object>> selector)
        {
            return config.AddAction(new NoopAction<A, B>(selector));
        }
    }
}