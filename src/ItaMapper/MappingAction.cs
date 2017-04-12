using System;
using System.Linq.Expressions;
using System.Reflection;
using ItaMapper.Exceptions;
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

    public class TargetFreeAction<Source, Destination> : MappingAction<Source, Destination>
    {
        private readonly Action<Source, Destination, ObjectInstantiator, Mapper, Context> action;

        public TargetFreeAction(Action<Source, Destination, ObjectInstantiator, Mapper, Context> action, int priority)
        {
            this.action = action;
            Priority = priority;
        }

        public void Map(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
            action(source, destination, instantiator, mapper, context);
        }

        public int Priority { get; }

        public string Target { get; } = null;
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
                .Pipe(value => mapper.Map(value.Object, value.Type, PropertyInfo.PropertyType, context))
                .Do(value => setter(destination, value));
        }

        public abstract TypedObject GetValue(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context);

        public int Priority { get; } = MappingPhase.Mapping;

        public string Target => PropertyInfo.Name;
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

    public class InlinePropertyMap<Source, Destination, Result> : PropertyMapAction<Source, Destination>
    {
        private readonly Func<PropertyMapArguments<Source, Destination>, Result> func;

        public InlinePropertyMap(Expression<Func<Destination, object>> expression, Func<PropertyMapArguments<Source, Destination>, Result> func) : base(expression)
        {
            this.func = func;
        }

        public override TypedObject GetValue(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
            return new PropertyMapArguments<Source, Destination>(source, destination, instantiator, mapper, PropertyInfo, context)
                .Pipe(func)
                .Pipe(value => new TypedObject(typeof(Result), value));
        }
    }

    public class InlinePropertyMap<Source, Destination> : PropertyMapAction<Source, Destination>
    {
        private readonly Func<PropertyMapArguments<Source, Destination>, TypedObject> func;

        public InlinePropertyMap(Expression<Func<Destination, object>> expression, Func<PropertyMapArguments<Source, Destination>, TypedObject> func) : base(expression)
        {
            this.func = func;
        }

        public override TypedObject GetValue(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
            return new PropertyMapArguments<Source, Destination>(source, destination, instantiator, mapper, PropertyInfo, context)
                .Pipe(func);
        }
    }

    public class DirectPropertyMap<Source, Destination> : PropertyMapAction<Source, Destination>
    {
        private readonly Func<object, object> getter;
        private readonly Type sourceProperty;

        public DirectPropertyMap(Expression<Func<Destination, object>> expression) : base(expression)
        {
            sourceProperty = typeof(Source).GetProperty(PropertyInfo.Name)?.PropertyType ?? throw new NoDirectMapTargetException(typeof(Source), PropertyInfo.Name);
            getter = new ExpressionBuilder().Getter(typeof(Source), PropertyInfo.Name);
        }

        public DirectPropertyMap(string name) : base(name)
        {
            sourceProperty = typeof(Source).GetProperty(PropertyInfo.Name)?.PropertyType ?? throw new NoDirectMapTargetException(typeof(Source), PropertyInfo.Name);
            getter = new ExpressionBuilder().Getter(typeof(Source), PropertyInfo.Name);
        }

        public override TypedObject GetValue(Source source, Destination destination, ObjectInstantiator instantiator, Mapper mapper, Context context)
        {
            return new TypedObject(sourceProperty, getter(source));
        }
    }
}