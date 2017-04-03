using System;

namespace ItaMapper
{
    public interface Mapper
    {
        object Map(object src, Type source, Type destination, Context context);
    }

    public static class MapperExtensions
    {
        public static B Map<A, B>(this Mapper mapper, A src, Context context) => (B)mapper.Map(src, typeof(A), typeof(B), context);

        public static B Map<A, B>(this Mapper mapper, A src) => mapper.Map<B>(src, new Context());

        public static B Map<B>(this Mapper mapper, object src, Context context) => (B)mapper.Map(src, src.GetType(), typeof(B), context);

        public static B Map<B>(this Mapper mapper, object src) => mapper.Map<B>(src, new Context());
    }
}