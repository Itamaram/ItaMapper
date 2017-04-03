using System;

namespace ItaMapper
{
    public interface ObjectInstantiator
    {
        object Create(Type type);
    }

    public class ActivatorInstantiator : ObjectInstantiator
    {
        public object Create(Type type) => Activator.CreateInstance(type);
    }
}