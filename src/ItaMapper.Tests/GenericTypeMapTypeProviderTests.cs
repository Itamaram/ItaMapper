using NUnit.Framework;
using System;

namespace ItaMapper.Tests
{
    public class GenericTypeMapTypeProviderTests
    {
        [TestCase(typeof(X<>), typeof(int), typeof(X<int>), typeof(int), typeof(X<>), typeof(X<int>))]
        [TestCase(typeof(int), typeof(X<>), typeof(int), typeof(X<int>), typeof(X<>), typeof(X<int>))]
        [TestCase(typeof(X<>), typeof(X<>), typeof(X<int>), typeof(X<int>), typeof(X<>), typeof(X<int>))]
        [TestCase(typeof(X<>), typeof(X<>), typeof(X<int>), typeof(X<int>), typeof(X<,>), typeof(X<int, int>))]
        [TestCase(typeof(X<,>), typeof(X<>), typeof(X<int, int>), typeof(X<int>), typeof(X<,,>), typeof(X<int, int, int>))]
        [TestCase(typeof(X<>), typeof(X<,>), typeof(X<int>), typeof(X<int, int>), typeof(X<,,>), typeof(X<int, int, int>))]
        public void SingleTypeTests(Type source, Type destination, Type src, Type dst, Type generic, Type expected)
        {
            Assert.AreEqual(expected, new GenericTypeMapTypeProvider(source, destination, generic).Create(src, dst));
        }

        private class X<A> { }
        private class X<A, B> { }
        private class X<A, B, C> { }
    }
}