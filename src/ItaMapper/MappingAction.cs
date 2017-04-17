using ItaMapper.Extensions;
using System;
using System.Reflection;

namespace ItaMapper
{
    public interface MappingAction<in Source, in Destination>
    {
        void Map(Source source, Destination destination, MappingContext context);
        int Priority { get; }

        //The target member (or null)
        string Target { get; }
    }

    public class NoopAction<Source, Destination> : MappingAction<Source, Destination>
    {
        public NoopAction(string target)
        {
            Target = target;
        }

        public void Map(Source source, Destination destination, MappingContext context)
        {
        }

        public int Priority { get; } = MappingPhase.Mapping;

        public string Target { get; }
    }

    public class TargetFreeAction<Source, Destination> : MappingAction<Source, Destination>
    {
        private readonly Action<Source, Destination, MappingContext> action;

        public TargetFreeAction(Action<Source, Destination, MappingContext> action, int priority)
        {
            this.action = action;
            Priority = priority;
        }

        public void Map(Source source, Destination destination, MappingContext context)
        {
            action(source, destination, context);
        }

        public int Priority { get; }

        public string Target { get; } = null;
    }

    public class ResolverMappingAction<A, B> : MappingAction<A, B>
    {
        private readonly Func<MappingContext, ValueResolver<A, B>> resolver;
        private readonly PropertyInfo property;
        private readonly Action<B, object> setter;
        
        //public ResolverMappingAction(string name, Func<MappingContext, ValueResolver<A, B>> resolver)
        //    : this(typeof(B).GetProperty(name), resolver)
        //{
        //}

        //public ResolverMappingAction(Expression<Func<B, object>> expression, Func<MappingContext, ValueResolver<A, B>> resolver)
        //    : this(expression.GetMemberExpression().GetPropertyInfo(), resolver)
        //{
        //}

        public ResolverMappingAction(PropertyInfo property, Func<MappingContext, ValueResolver<A, B>> resolver)
        {
            this.property = property;
            this.resolver = resolver;
            Target = property.Name;
            setter = new ExpressionBuilder().Setter<B>(property);
        }

        public void Map(A source, B destination, MappingContext context)
        {
            var r = resolver(context);
            r.Resolve(new PropertyMapArguments<A, B>(source, destination, property, context))
                .Pipe(v => context.Mapper.Map(v, r.MemberType, property.PropertyType, context.State))
                .Do(v => setter(destination, v));
        }

        public int Priority { get; } = MappingPhase.Mapping;

        public string Target { get; }
    }

    public class TypedObject
    {
        public TypedObject(Type type, object obj)
        {
            Type = type;
            Object = obj;
        }

        public Type Type { get; }
        public object Object { get; }
    }

    public class PropertyMapArguments<A, B>
    {
        public PropertyMapArguments(A source, B destination, PropertyInfo pi, MappingContext context)
        {
            Source = source;
            Destination = destination;
            PropertyInfo = pi;
            Context = context;
        }

        public A Source { get; }
        public B Destination { get; }
        public PropertyInfo PropertyInfo { get; }
        public MappingContext Context { get; }
    }
}