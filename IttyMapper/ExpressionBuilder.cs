using System;
using System.Linq.Expressions;
using IttyMapper.Extensions;

namespace IttyMapper
{
    public class ExpressionBuilder
    {
        public Func<object, object> Getter(Type type, string name)
        {
            var target = Expression.Parameter(typeof(object), "x");
            var property = Expression.Property(target.Convert(type), name);

            return Expression.Lambda<Func<object, object>>(property, target).Compile();
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
    }
}