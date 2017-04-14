using System;
using System.Collections.Generic;

namespace ItaMapper
{
    public class MappingState : Dictionary<string, object> { }

    public class MappingContext
    {
        public MappingContext(Type source, Type destination, Mapper mapper, ObjectInstantiator instantiator, MappingState state)
        {
            Source = source;
            Destination = destination;
            Mapper = mapper;
            Instantiator = instantiator;
            State = state;
        }

        public MappingState State { get; }
        public Type Source { get; }
        public Type Destination { get; }
        public Mapper Mapper { get; }
        public ObjectInstantiator Instantiator { get; }
    }
}