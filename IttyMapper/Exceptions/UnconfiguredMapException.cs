using System;

namespace IttyMapper.Exceptions
{
    public class UnconfiguredMapException : Exception
    {
        public UnconfiguredMapException(Type source, Type destination)
            :base($"Unable to map from '{source}' to '{destination}', no map is configured")
        {
        }
    }
}