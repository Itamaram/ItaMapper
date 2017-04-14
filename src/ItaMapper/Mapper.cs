using System;

namespace ItaMapper
{
    public interface Mapper
    {
        object Map(object src, Type source, Type destination, MappingState state);
    }

    public static class MapperExtensions
    {
        public static B Map<A, B>(this Mapper mapper, A src, MappingState state) => (B)mapper.Map(src, typeof(A), typeof(B), state);

        public static B Map<A, B>(this Mapper mapper, A src) => mapper.Map<B>(src, new MappingState());

        public static B Map<B>(this Mapper mapper, object src, MappingState state) => (B)mapper.Map(src, src.GetType(), typeof(B), state);

        public static B Map<B>(this Mapper mapper, object src) => mapper.Map<B>(src, new MappingState());
    }
}