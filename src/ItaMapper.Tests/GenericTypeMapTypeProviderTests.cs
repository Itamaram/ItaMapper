using NUnit.Framework;
using System;

namespace ItaMapper.Tests
{
    public class GenericTypeMapTypeProviderTests
    {
        [TestCase(typeof(X<int>), typeof(int), typeof(X<>), typeof(X<int>))]
        [TestCase(typeof(int), typeof(X<int>), typeof(X<>), typeof(X<int>))]
        [TestCase(typeof(X<int>), typeof(X<int>), typeof(X<>), typeof(X<int>))]
        [TestCase(typeof(X<int>), typeof(X<int>), typeof(X<,>), typeof(X<int, int>))]
        [TestCase(typeof(X<int, int>), typeof(X<int>), typeof(X<,,>), typeof(X<int, int, int>))]
        [TestCase(typeof(X<int>), typeof(X<int, int>), typeof(X<,,>), typeof(X<int, int, int>))]
        public void SingleTypeTests(Type src, Type dst, Type generic, Type expected)
        {
            Assert.AreEqual(expected, new GenericTypeMapTypeProvider(generic).Create(src, dst));
        }

        [TestCase(typeof(X<int>), typeof(X<int>), typeof(X<>), typeof(X<,>), typeof(X<int>))]
        [TestCase(typeof(X<int>), typeof(X<string>), typeof(X<>), typeof(X<,>), typeof(X<int, string>))]
        public void DoubleTypeTests(Type src, Type dst, Type generic, Type fallback, Type expected)
        {
            Assert.AreEqual(expected, new GenericTypeMapTypeProvider(generic, fallback).Create(src, dst));
        }

        private class X<A> { }
        private class X<A, B> { }
        private class X<A, B, C> { }
    }
}