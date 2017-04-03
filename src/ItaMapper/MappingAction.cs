using System;
using System.Linq.Expressions;
using System.Reflection;
using ItaMapper.Extensions;

namespace ItaMapper
{
    public interface MappingAction<in Source, in Destination>
    {
        void Map(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context);
        int Priority { get; }

        //The target member (or null)
        string Target { get; }
    }

    public class NoopAction<Source, Destination> : MappingAction<Source, Destination>
    {
        public NoopAction(Expression<Func<Destination, object>> expression)
        {
            Target = expression.GetMemberExpression().GetPropertyInfo().Name;
        }

        public void Map(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
        }

        public int Priority { get; } = MappingPhase.Mapping;

        public string Target { get; } 
    }

    public abstract class PropertyMapAction<Source, Destination> : MappingAction<Source, Destination>
    {
        private readonly Action<object, object> setter;
        protected readonly PropertyInfo PropertyInfo;

        protected PropertyMapAction(Expression<Func<Destination, object>> expression)
        {
            PropertyInfo = expression.GetMemberExpression().GetPropertyInfo();
            setter = new ExpressionSetterFactory().SetterFor(typeof(Destination), PropertyInfo.Name);
        }

        protected PropertyMapAction(string name)
        {
            PropertyInfo = typeof(Destination).GetProperty(name);
            setter = new ExpressionSetterFactory().SetterFor(typeof(Destination), PropertyInfo.Name);
        }

        public void Map(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
            GetValue(source, destination, instantiator, mapper, context)
                .Do(value => setter(destination, value));
        }

        public abstract object GetValue(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context);

        public int Priority { get; } = MappingPhase.Mapping;

        public string Target => PropertyInfo.Name;
    }

    public class PropertyMapArguments<A, B>
    {
        public PropertyMapArguments(A source, B destination, ObjectInstantiator instantiator, Mapper mapper, PropertyInfo pi, Context context)
        {
            Source = source;
            Destination = destination;
            ObjectInstantiator = instantiator;
            Mapper = mapper;
            PropertyInfo = pi;
            Context = context;
        }

        public A Source { get; }
        public B Destination { get; }
        public ObjectInstantiator ObjectInstantiator { get; }
        public Mapper Mapper { get; }
        public PropertyInfo PropertyInfo { get; }
        public Context Context { get; }
    }

    public class InlinePropertyMap<Source, Destination> : PropertyMapAction<Source, Destination>
    {
        private readonly Func<PropertyMapArguments<Source, Destination>, object> func;

        public InlinePropertyMap(Expression<Func<Destination, object>> expression, Func<PropertyMapArguments<Source, Destination>, object> func) : base(expression)
        {
            this.func = func;
        }

        public override object GetValue(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
            return func(new PropertyMapArguments<Source, Destination>(source, destination, instantiator, mapper, PropertyInfo, context));
        }
    }

    public class DirectPropertyMap<Source, Destination> : PropertyMapAction<Source, Destination>
    {
        private readonly Func<object, object> getter;
        private readonly Type sourceProperty;

        public DirectPropertyMap(Expression<Func<Destination, object>> expression) : base(expression)
        {
            getter = new ExpressionBuilder().Getter(typeof(Source), PropertyInfo.Name);
            sourceProperty = typeof(Source).GetProperty(PropertyInfo.Name).PropertyType;
        }

        public DirectPropertyMap(string name) : base(name)
        {
            getter = new ExpressionBuilder().Getter(typeof(Source), PropertyInfo.Name);
            sourceProperty = typeof(Source).GetProperty(PropertyInfo.Name).PropertyType;
        }

        public override object GetValue(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
            return getter(source)
                .Map(value => mapper.Map(value, sourceProperty, PropertyInfo.PropertyType, context));
        }
    }
}