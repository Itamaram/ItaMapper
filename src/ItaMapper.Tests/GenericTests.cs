using NUnit.Framework;

namespace ItaMapper.Tests
{
    public class Foo<T>
    {
        public T Value { get; set; }
    }

    public class Bar<T>
    {
        public T Value { get; set; }
    }

    public class FooBarTypeMap<A> : ActionAggregateTypeMap<Foo<A>, Bar<A>>
    {
        public FooBarTypeMap() : base(config)
        {
        }

        private static readonly TypeMapConfig<Foo<A>, Bar<A>> config =
            new TypeMapConfig<Foo<A>, Bar<A>>()
                .Map(b => b.Value).From(f => f.Value);
    }

    public class FooBarTypeMap<A, B> : ActionAggregateTypeMap<Foo<A>, Bar<B>>
    {
        public FooBarTypeMap() : base(config)
        {
        }

        private static readonly TypeMapConfig<Foo<A>, Bar<B>> config =
            new TypeMapConfig<Foo<A>, Bar<B>>()
                .Map(b => b.Value).From(f => f.Value);
    }

    public class GenericTests
    {
        [Test]
        public void SingleTypeTest()
        {
            var mapper = new BareBoneMapper(new[]
            {
                CreateTypeMap.From(typeof(Foo<>)).To(typeof(Bar<>)).Using(typeof(FooBarTypeMap<>)),
                new FuncTypeMap<string, string>((s, _) => s),
            }, new ActivatorInstantiator());

            Assert.AreEqual("X", mapper.Map<Bar<string>>(new Foo<string> { Value = "X" }).Value);
        }

        [Test]
        public void DoubleTypeTest()
        {
            var mapper = new BareBoneMapper(new[]
            {
                CreateTypeMap.From(typeof(Foo<>)).To(typeof(Bar<>)).Using(typeof(FooBarTypeMap<,>)),
                new FuncTypeMap<string, string>((s, _) => s),
            }, new ActivatorInstantiator());

            Assert.AreEqual("X", mapper.Map<Bar<string>>(new Foo<string> { Value = "X" }).Value);
        }

        [Test]
        public void DoubleToSingleTypeTest()
        {
            var mapper = new BareBoneMapper(new[]
            {
                CreateTypeMap.From(typeof(Foo<>)).To(typeof(Bar<>)).Using(typeof(FooBarTypeMap<,>)),
                new FuncTypeMap<int, string>((s, _) => $"{s}"),
            }, new ActivatorInstantiator());

            Assert.AreEqual("1", mapper.Map<Bar<string>>(new Foo<int> { Value = 1 }).Value);
        }
    }
}