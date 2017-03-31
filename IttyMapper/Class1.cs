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

    public class IttyMapper : Mapper
    {
        private readonly IoC ioc;
        private readonly TypeMapDictionary maps;

        public IttyMapper(IEnumerable<TypeMapConfig> maps)
            : this(Activate, maps) { }

        private static object Activate(Type t) => Activator.CreateInstance(t);

        public IttyMapper(IoC ioc, IEnumerable<TypeMapConfig> maps)
        {
            this.ioc = ioc;
            this.maps = new TypeMapDictionary(maps.Select(m => new TypeMap(m, new ExpressionSetterFactory())));
        }

        public object Map(object src, Type source, Type destination, Context context)
        {
            if (maps.TryGet(source, destination, out var map))
                return map.Map(src, ioc, this, context);

            throw new Exception("Missing type map");
        }
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
    public interface TypeMapConfig
    {
        Type Source { get; }
        Type Destination { get; }
        IReadOnlyDictionary<string, MemberMap> MemberMaps { get; }
        IEnumerable<MutateDestination> BeforeMap { get; }
        IEnumerable<MutateDestination> AfterMap { get; }
    }

    public class TypeMapConfig<A, B> : TypeMapConfig
    {
        public Type Source { get; } = typeof(A);
        public Type Destination { get; } = typeof(B);

        private readonly Dictionary<string, MemberMap> configs = new Dictionary<string, MemberMap>();
        public IReadOnlyDictionary<string, MemberMap> MemberMaps => configs;

        private readonly List<MutateDestination> before = new List<MutateDestination>();
        public IEnumerable<MutateDestination> BeforeMap => before;

        private readonly List<MutateDestination> after = new List<MutateDestination>();
        public IEnumerable<MutateDestination> AfterMap => after;

        public TypeMapConfig<A, B> Before(MutateDestination m)
        {
            before.Add(m);
            return this;
        }

        public TypeMapConfig<A, B> After(MutateDestination m)
        {
            after.Add(m);
            return this;
        }

        public TypeMapConfig<A, B> AddMap(MemberMap map)
        {
            configs.Add(map.TargetMember.Name, map);
            return this;
        }
    }

    public static class TypeMapConfigExtensions
    {
        public static TypeMapConfig<A, B> MapRemainingProperties<A, B>(this TypeMapConfig<A, B> config)
        {
            typeof(B).GetProperties()
                .Where(p => !config.MemberMaps.ContainsKey(p.Name))
                .ForEach(p => config.AddMap(new ReflectionBasedMemberMap<A>(p.Name)));

            return config;
        }
    }

    public delegate void MutateDestination(ref object dst, Context context);

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

    public interface MappingAction
    {
        void Invoke(object source, object destination, IoC ioc, Mapper mapper, Context context);
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

    public abstract class PropertyMapAction<Destination> : MappingAction
    {
        private readonly Action<object, object> setter;
        protected readonly MemberInfo MemberInfo;

        protected PropertyMapAction(Expression<Func<Destination, object>> expression)
        {
            MemberInfo = ((MemberExpression)expression.Body).Member;
            setter = new ExpressionSetterFactory().SetterFor(typeof(Destination), MemberInfo.Name);
        }

        public void Invoke(object source, object destination, IoC ioc, Mapper mapper, Context context)
        {
            //Potentially type check for further map here?
            GetValue(source, destination, ioc, mapper, context).Do(value => setter(destination, value));
        }

        public abstract object GetValue(object source, object destination, IoC ioc, Mapper mapper, Context context);

        public int Priority { get; } = MappingPhase.Mapping;

        public string Target => MemberInfo.Name;
    }

    public class DirectPropertyMap<Source, Destination> : PropertyMapAction<Destination>
    {
        private readonly Func<object, object> getter;

        public DirectPropertyMap(Expression<Func<Destination, object>> expression) : base(expression)
        {
            getter = new ExpressionBuilder().Getter(typeof(Source), MemberInfo.Name);
        }

        public override object GetValue(object source, object destination, IoC ioc, Mapper mapper, Context context)
        {
            return getter(source);
        }
    }

    public class TypeMap
    {
        private readonly TypeMapConfig config;
        public Type Source => config.Source;
        public Type Destination => config.Destination;

        private readonly IReadOnlyDictionary<string, Action<object, object>> setters;

        public TypeMap(TypeMapConfig config, SimpleSetterFactory factory)
        {
            this.config = config;
            setters = config.MemberMaps.Keys.ToDictionary(x => x, x => factory.SetterFor(config.Destination, x));
        }

        public object Map(object source, IoC ioc, Mapper mapper, Context context)
        {
            var dst = ioc(config.Destination);

            foreach (var before in config.BeforeMap)
                before(ref dst, context);

            foreach (var map in config.MemberMaps)
            {
                var value = map.Value.GetValue(source, dst, ioc, mapper, context);

                //null check!
                if (value.GetType() != map.Value.TargetMember.PropertyType)
                    value = mapper.Map(value, value.GetType(), map.Value.TargetMember.PropertyType, context);

                setters[map.Key].Invoke(dst, value);
            }

            foreach (var after in config.AfterMap)
                after(ref dst, context);

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