using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace ItaMapper.Tests
{
    public class ValueTupleTypeMapDictionaryTests
    {
        private class MockMap : TypeMap
        {
            public MockMap(Type source, Type destination)
            {
                Source = source;
                Destination = destination;
            }

            public Type Source { get; }
            public Type Destination { get; }
            public object Map(object source, MappingContext context) => null;
        }

        [TestCase(typeof(string), typeof(int))]
        [TestCase(typeof(string), typeof(string))]
        [TestCase(typeof(List<>), typeof(int))]
        [TestCase(typeof(List<>), typeof(List<>))]
        public void Fetch(Type source, Type destination)
        {
            var map = new MockMap(source, destination);
            var dictionary = new ValueTupleTypeMapDictionary(new[] { map });

            Assert.True(dictionary.TryGet(source, destination, out var actual));
            Assert.AreEqual(map, actual);
        }

        [Test]
        public void FailToFetch()
        {
            var dictionary = new ValueTupleTypeMapDictionary(Enumerable.Empty<TypeMap>());
            Assert.False(dictionary.TryGet(typeof(int), typeof(string), out var _));
        }

        [Test]
        public void GenericOverOpenGeneric()
        {
            var open = new MockMap(typeof(List<>), typeof(List<>));
            var closed = new MockMap(typeof(List<int>), typeof(List<int>));

            var dictionary = new ValueTupleTypeMapDictionary(new[] { open, closed });

            Assert.True(dictionary.TryGet(typeof(List<int>), typeof(List<int>), out var actual));
            Assert.AreEqual(closed, actual);

            Assert.True(dictionary.TryGet(typeof(List<int>), typeof(List<string>), out actual));
            Assert.AreEqual(open, actual);
        }
    }
}