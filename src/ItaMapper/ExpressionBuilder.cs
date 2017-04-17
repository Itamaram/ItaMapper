using System;
using System.Linq.Expressions;
using System.Reflection;
using ItaMapper.Extensions;

namespace ItaMapper
{
    public class ExpressionBuilder
    {
        public Func<object, object> Getter(Type type, string name)
        {
            var target = Expression.Parameter(typeof(object), "x");
            var property = Expression.Property(target.Convert(type), name);

            return Expression.Lambda<Func<object, object>>(property, target).Compile();
        }

        public Func<A, B> Getter<A, B>(string name)
        {
            var target = Expression.Parameter(typeof(A), "x");
            var property = Expression.Property(target, name);

            return Expression.Lambda<Func<A, B>>(property, target).Compile();
        }

        public Action<object, object> Setter(Type type, string name)
        {
            //equivalent to (object x, object value) => ((type) x).name = (propertytype) value

            var pi = type.GetProperty(name);
            var target = Expression.Parameter(typeof(object), "x");
            var value = Expression.Parameter(typeof(object), "value");
            var assign = Expression.Assign(Expression.Property(target.Convert(type), name), value.Convert(pi.PropertyType));

            return Expression.Lambda<Action<object, object>>(assign, target, value).Compile();
        }

        public Action<A, object> Setter<A>(string name) => Setter<A>(typeof(A).GetProperty(name));

        public Action<A, object> Setter<A>(PropertyInfo pi)
        {
            var target = Expression.Parameter(typeof(A), "x");
            var value = Expression.Parameter(typeof(object), "value");
            var assign = Expression.Assign(Expression.Property(target, pi), value.Convert(pi.PropertyType));

            return Expression.Lambda<Action<A, object>>(assign, target, value).Compile();
        }

        public Action<A, B> Setter<A, B>(string name) => Setter<A, B>(typeof(A).GetProperty(name));

        public Action<A, B> Setter<A, B>(PropertyInfo pi)
        {
            var target = Expression.Parameter(typeof(A), "x");
            var value = Expression.Parameter(typeof(B), "value");
            var assign = Expression.Assign(Expression.Property(target, pi), value);

            return Expression.Lambda<Action<A, B>>(assign, target, value).Compile();
        }
    }
}