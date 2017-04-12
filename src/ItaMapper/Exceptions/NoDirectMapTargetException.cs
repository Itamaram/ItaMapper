using System;

namespace ItaMapper.Exceptions
{
    public class NoDirectMapTargetException : Exception
    {
        public NoDirectMapTargetException(Type source, string name)
            : base($"Type '{source}' does not have a property '{name}' for a direct map") { }
    }
}