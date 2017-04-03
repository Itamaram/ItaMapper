using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace IttyMapper
{
    public interface Mapper
    {
        object Map(object src, Type source, Type destination, Context context);
    }

    public class BareMetalMapper : Mapper
    {
        private readonly IoC ioc;
        private readonly TypeMapDictionary maps;

        public BareMetalMapper(IEnumerable<TypeMap> maps)
            : this(maps, Activate) { }

        private static object Activate(Type t) => Activator.CreateInstance(t);

        public BareMetalMapper(IEnumerable<TypeMap> maps, IoC ioc)
        {
            this.ioc = ioc;
            this.maps = new TypeMapDictionary(maps);
        }

        public object Map(object src, Type source, Type destination, Context context)
        {
            if (maps.TryGet(source, destination, out var map))
                return map.Map(src, ioc, this, context);

            throw new Exception("Missing type map");
        }
    }

    public class IttyMapper : BareMetalMapper
    {
        public IttyMapper(IEnumerable<TypeMap> maps) : base(maps.Concat(defaults))
        {
        }

        public IttyMapper(IEnumerable<TypeMap> maps, IoC ioc) : base(maps.Concat(defaults), ioc)
        {
        }

        private static readonly IEnumerable<TypeMap> defaults = new[]
        {
            new CloneTypeMap<string>(),
        };
    }

    public class TypeMapDictionary
    {
        private readonly IReadOnlyDictionary<Tuple<Type, Type>, TypeMap> store;

        public TypeMapDictionary(IEnumerable<TypeMap> maps)
        {
            store = maps.ToDictionary(m => Tuple.Create(m.Source, m.Destination), new TypeTupleComparer());
        }

        public bool TryGet(Type source, Type destination, out TypeMap map)
        {
            return store.TryGetValue(Tuple.Create(source, destination), out map);
        }
    }

    public class TypeTupleComparer : IEqualityComparer<Tuple<Type, Type>>
    {
        public bool Equals(Tuple<Type, Type> x, Tuple<Type, Type> y)
        {
            return x.Item1 == y.Item1 && x.Item2 == y.Item2;
        }

        public int GetHashCode(Tuple<Type, Type> obj)
        {
            return obj.Item1.GetHashCode() ^ obj.Item2.GetHashCode();
        }
    }

    public static class MapperExtensions
    {
        public static B Map<A, B>(this Mapper mapper, A src, Context context) => (B)mapper.Map(src, typeof(A), typeof(B), context);

        public static B Map<A, B>(this Mapper mapper, A src) => mapper.Map<B>(src, new Context());

        public static B Map<B>(this Mapper mapper, object src, Context context) => (B)mapper.Map(src, src.GetType(), typeof(B), context);

        public static B Map<B>(this Mapper mapper, object src) => mapper.Map<B>(src, new Context());
    }

    public class Context : Dictionary<string, object>
    {
    }

    public delegate object IoC(Type type);


    //With extensions like .MapRemainingMembers
    //This is thte wrong abstraction?
    public class TypeMapConfig<A, B> 
    {
        private readonly HashSet<string> targets;

        public TypeMapConfig()
        {
            Actions = new List<MappingAction<A,B>>();
            targets = new HashSet<string>();
        }

        private TypeMapConfig(IEnumerable<MappingAction<A,B>> actions, IEnumerable<string> targets)
        {
            Actions = actions.ToList();
            this.targets = new HashSet<string>(targets);
        }

        public TypeMapConfig<A, B> AddAction(MappingAction<A,B> action)
        {
            return new TypeMapConfig<A, B>(Actions.Append(action), targets.AppendIfNotNull(action.Target));
        }

        public IEnumerable<MappingAction<A,B>> Actions { get; }

        public IEnumerable<string> Targets => targets;

        public bool Targeting(string target) => targets.Contains(target);
    }

    public static class TypeMapConfigExtensions
    {
        public static TypeMapConfig<A, B> MapRemainingProperties<A, B>(this TypeMapConfig<A, B> config)
        {
            return typeof(B).GetProperties()
                .Where(p => !config.Targeting(p.Name))
                .Aggregate(config, (c, p) => c.AddAction(new DirectPropertyMap<A, B>(p.Name)));
        }
    }

    public interface SimpleSetterFactory
    {
        Action<object, object> SetterFor(Type type, string member);
    }

    public class ReflectionSetterFactory : SimpleSetterFactory
    {
        public Action<object, object> SetterFor(Type type, string member)
        {
            return type.GetProperty(member).SetMethod.Map(Invoke);
        }

        private static Action<object, object> Invoke(MethodInfo mi) => (x, y) => mi.Invoke(x, new[] { y });
    }

    public class ExpressionSetterFactory : SimpleSetterFactory
    {
        public Action<object, object> SetterFor(Type type, string member)
        {
            return new ExpressionBuilder().Setter(type, member);
        }
    }

    internal static class FluentExtensions
    {
        public static A Do<A>(this A a, Action<A> action)
        {
            action(a);
            return a;
        }

        public static B Map<A, B>(this A a, Func<A, B> map) => map(a);
    }

    public interface MappingAction<in Source, in Destination>
    {
        void Map(Source source, Destination destination, IoC ioc, Mapper mapper, Context context);
        int Priority { get; }

        //The target member (or null)
        string Target { get; }
    }

    public static class MappingPhase
    {
        public static int BeforeMapping = 0;
        public static int Mapping = 1;
        public static int AfterMapping = 2;
    }

    public abstract class PropertyMapAction<Source, Destination> : MappingAction<Source, Destination>
    {
        private readonly Action<object, object> setter;
        protected readonly PropertyInfo PropertyInfo;

        protected PropertyMapAction(Expression<Func<Destination, object>> expression)
        {
            PropertyInfo = (PropertyInfo)((MemberExpression)expression.Body).Member;
            setter = new ExpressionSetterFactory().SetterFor(typeof(Destination), PropertyInfo.Name);
        }

        protected PropertyMapAction(string name)
        {
            PropertyInfo = typeof(Destination).GetProperty(name);
            setter = new ExpressionSetterFactory().SetterFor(typeof(Destination), PropertyInfo.Name);
        }

        public void Map(Source source, Destination destination, IoC ioc, Mapper mapper, Context context)
        {
            GetValue(source, destination, ioc, mapper, context)
                .Do(value => setter(destination, value));
        }

        public abstract object GetValue(Source source, Destination destination, IoC ioc, Mapper mapper, Context context);

        public int Priority { get; } = MappingPhase.Mapping;

        public string Target => PropertyInfo.Name;
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

        public override object GetValue(Source source, Destination destination, IoC ioc, Mapper mapper, Context context)
        {
            return getter(source)
                .Map(value => mapper.Map(value, sourceProperty, PropertyInfo.PropertyType, context));
        }
    }

    public interface TypeMap
    {
        Type Source { get; }
        Type Destination { get; }
        object Map(object source, IoC ioc, Mapper mapper, Context context);
    }

    public abstract class TypeMap<TSource, TDestination> : TypeMap
    {
        public Type Source { get; } = typeof(TSource);

        public Type Destination { get; } = typeof(TDestination);

        public object Map(object source, IoC ioc, Mapper mapper, Context context)
        {
            return Map((TSource)source, ioc, mapper, context);
        }

        protected abstract TDestination Map(TSource source, IoC ioc, Mapper mapper, Context context);
    }

    public class CloneTypeMap<A> : TypeMap
    {
        public Type Source { get; } = typeof(A);

        public Type Destination { get; } = typeof(A);

        public object Map(object source, IoC ioc, Mapper mapper, Context context) => source;
    }

    public class ActionAggregateTypeMap<TSource, TDestination> : TypeMap<TSource, TDestination>
    {
        private readonly IReadOnlyList<MappingAction<TSource, TDestination>> actions;

        public ActionAggregateTypeMap(TypeMapConfig<TSource, TDestination> config)
        {
            actions = config.Actions.OrderBy(a => a.Priority).ToList();
        }

        protected override TDestination Map(TSource source, IoC ioc, Mapper mapper, Context context)
        {
            var dst = (TDestination) ioc(Destination);

            foreach (var action in actions)
                action.Map(source, dst, ioc, mapper, context);

            return dst;
        }
    }

    public interface MemberMap //start with naive reflection implementation, then expression trees
    {
        FieldOrPropertyInfo TargetMember { get; }
        object GetValue(object src, object destination, IoC ioc, Mapper mapper, Context context);
    }

    public class ReflectionBasedMemberMap<A> : MemberMap
    {
        public Type Source { get; } = typeof(A);

        public FieldOrPropertyInfo TargetMember { get; }

        private readonly Func<object, object> getter;

        public ReflectionBasedMemberMap(Expression<Func<A, object>> expression)
        {
            TargetMember = expression.GetFieldOrPropertyInfo();
            getter = TargetMember.ReflectionGetter();
        }

        public object GetValue(object src, object destination, IoC ioc, Mapper mapper, Context context)
        {
            return getter(src);
        }
    }

    public class ExpressionBasedMemberMap<A> : MemberMap
    {
        public Type Source { get; } = typeof(A);

        public FieldOrPropertyInfo TargetMember { get; }

        private readonly Func<object, object> getter;

        public ExpressionBasedMemberMap(string name)
        {
            TargetMember = new FieldOrPropertyInfoFactory().Get(typeof(A), name);
            getter = new ExpressionBuilder().Getter(Source, name);
        }

        public object GetValue(object src, object destination, IoC ioc, Mapper mapper, Context context)
        {
            return getter(src);
        }
    }

    public class ExpressionBuilder
    {
        public Func<object, object> Getter(Type type, string name)
        {
            var target = Expression.Parameter(typeof(object), "x");
            var property = Expression.Property(target.Convert(type), name);

            return Expression.Lambda<Func<object, object>>(property, target).Compile();
        }

        public Func<object, object> FieldGetter(Type type, string name)
        {
            var target = Expression.Parameter(typeof(object), "x");
            var field = Expression.Field(target.Convert(type), name);

            return Expression.Lambda<Func<object, object>>(field, target).Compile();
        }

        public Action<object, object> Setter(Type type, string name)
        {
            //equivalent to (object x, object value) => ((type) x).name = (propertytype) value

            var pi = type.GetProperty(name);
            var target = Expression.Parameter(typeof(object), "x");
            var value = Expression.Parameter(typeof(object), "value");
            var assign = Expression.Assign(Expression.PropertyOrField(target.Convert(type), name), value.Convert(pi.PropertyType));

            return Expression.Lambda<Action<object, object>>(assign, target, value).Compile();
        }
    }

    public abstract class FieldOrPropertyInfo : MemberInfo
    {
        private readonly MemberInfo mi;

        protected FieldOrPropertyInfo(MemberInfo mi)
        {
            this.mi = mi;
        }

        public abstract Type FieldOrPropertyType { get; }

        public abstract MemberExpression ExpressionGetter(Expression expression);

        public abstract Func<object, object> ReflectionGetter();

        #region sealed overrides
        public sealed override object[] GetCustomAttributes(bool inherit) => mi.GetCustomAttributes(inherit);

        public sealed override bool IsDefined(Type attributeType, bool inherit) => mi.IsDefined(attributeType, inherit);

        public sealed override MemberTypes MemberType => mi.MemberType;

        public sealed override string Name => mi.Name;

        public sealed override Type DeclaringType => mi.DeclaringType;

        public sealed override Type ReflectedType => mi.ReflectedType;

        public sealed override object[] GetCustomAttributes(Type attributeType, bool inherit) => mi.GetCustomAttributes(attributeType, inherit);
        #endregion
    }

    internal class FieldFieldOrPropertyInfo : FieldOrPropertyInfo
    {
        private readonly FieldInfo fi;

        public FieldFieldOrPropertyInfo(FieldInfo fi) : base(fi)
        {
            this.fi = fi;
        }

        public override Type FieldOrPropertyType => fi.FieldType;

        public override MemberExpression ExpressionGetter(Expression expression)
        {
            return Expression.Field(expression, fi.Name);
        }

        public override Func<object, object> ReflectionGetter()
        {
            return fi.GetValue;
        }
    }

    internal class PropertyFieldOrPropertyInfo : FieldOrPropertyInfo
    {
        private readonly PropertyInfo pi;

        public PropertyFieldOrPropertyInfo(PropertyInfo pi) : base(pi)
        {
            this.pi = pi;
        }

        public override Type FieldOrPropertyType => pi.PropertyType;

        public override MemberExpression ExpressionGetter(Expression expression)
        {
            return Expression.Property(expression, pi.Name);
        }

        public override Func<object, object> ReflectionGetter()
        {
            var mi = pi.GetMethod;
            return o => mi.Invoke(0, null);
        }
    }

    public static class Extensions
    {
        public static UnaryExpression Convert(this Expression e, Type t)
        {
            return Expression.Convert(e, t);
        }

        public static void ForEach<A>(this IEnumerable<A> items, Action<A> action)
        {
            foreach (var item in items)
                action(item);
        }

        public static FieldOrPropertyInfo GetFieldOrPropertyInfo<A>(this Expression<Func<A, object>> expression)
        {
            if (!(expression.Body is MemberExpression me))
                throw new Exception("Expression body was not a simple getter");

            if (me.Member is PropertyInfo pi)
                return new PropertyFieldOrPropertyInfo(pi);

            if (me.Member is FieldInfo fi)
                return new FieldFieldOrPropertyInfo(fi);

            throw new Exception("Only supported members are fields, properties.");
        }

        public static Action<object, object> SetterFor<A>(this SimpleSetterFactory factory, string member)
        {
            return factory.SetterFor(typeof(A), member);
        }

        public static IEnumerable<A> Yield<A>(this A a)
        {
            yield return a;
        }

        public static IEnumerable<A> Append<A>(this IEnumerable<A> items, A item)
        {
            return items.Concat(item.Yield());
        }

        public static IEnumerable<A> AppendIfNotNull<A>(this IEnumerable<A> items, A item)
            where A : class
        {
            return items.Concat(item.Yield().Where(a => a != null));
        }
    }

    public class FieldOrPropertyInfoFactory
    {
        public FieldOrPropertyInfo Get<A>(Expression<Func<A, object>> expression)
        {
            if (!(expression.Body is MemberExpression me))
                throw new Exception("Expression body was not a simple getter");

            if (me.Member is PropertyInfo pi)
                return new PropertyFieldOrPropertyInfo(pi);

            if (me.Member is FieldInfo fi)
                return new FieldFieldOrPropertyInfo(fi);

            throw new Exception("Only supported members are fields, properties.");
        }

        //Hidden properties might one day become an issue
        public FieldOrPropertyInfo Get(Type type, string name)
        {
            var pi = type.GetProperty(name);
            if (pi != null)
                return new PropertyFieldOrPropertyInfo(pi);

            var fi = type.GetField(name);
            if (fi != null)
                return new FieldFieldOrPropertyInfo(fi);

            throw new Exception("Only supported members are fields, properties.");
        }
    }
}